using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.App.Composition;
using CopyGIF.App.Views;
using CopyGIF.Application.Settings;
using CopyGIF.Core.Settings;
using CopyGIF.Platform.Windows.Shell;
using CopyGIF.Presentation.Settings;
using Microsoft.UI.Xaml.Controls;

namespace CopyGIF.App.Services;

/// <summary>Owns a draft for the lifetime of one Settings window, including while hidden.</summary>
internal sealed class SettingsWindowController : IDisposable
{
    private readonly SettingsWindow _window;
    private readonly SettingsViewModel _model;
    private readonly SettingsEditSession _session;
    private readonly ProviderKeysViewModel _keys;
    private readonly ShellRuntimeState _runtime;
    private bool _loading = true;
    private bool _disposed;
    private bool _dialogOpen;
    private CancellationTokenSource? _successStatusTimer;
    public bool IsDialogOpen => _dialogOpen;

    public SettingsWindowController(SettingsWindow window, SettingsViewModel model,
        SettingsEditSession session, ShellRuntimeState runtime)
    {
        _window = window; _model = model; _session = session; _runtime = runtime;
        _keys = new ProviderKeysViewModel(session);
        window.ApiContent = _keys;
        window.SaveCommand = new AsyncRelayCommand(async () => { await ApplyAsync(); });
        window.CanCloseAsync = ResolveCloseAsync;
        window.IsInteractionProtected = () => _dialogOpen || _loading || window.IsBusy;
        model.Library.PickFolder = async (initial, token) =>
        {
            _dialogOpen = true;
            try
            {
                var picker = new FolderPickerService(new Owner(window));
                return await picker.PickFolderAsync(initial, token);
            }
            finally { _dialogOpen = false; }
        };
        model.Library.OpenFolder = path => Task.FromResult(ShellFolderLauncher.TryOpen(path));
        foreach (INotifyPropertyChanged section in Sections()) section.PropertyChanged += Changed;
        session.Changed += SessionChanged;
    }

    public async Task LoadAsync()
    {
        _window.IsBusy = true;
        try
        {
            await _session.BeginAsync();
            await _model.LoadCommand.ExecuteAsync(null);
            if (!_model.IsLoaded || _model.HasSectionErrors)
                throw new InvalidOperationException("Settings could not be loaded. Close and reopen Settings to retry.");
            await _keys.RefreshAsync();
            _loading = false;
            CaptureDraft();
            _window.StatusMessage = string.Empty;
        }
        catch (Exception exception)
        {
            _window.StatusMessage = exception.Message;
            _window.StatusSeverity = InfoBarSeverity.Error;
        }
        finally { _window.IsBusy = false; }
    }

    private IEnumerable<INotifyPropertyChanged> Sections() =>
        [_model.General, _model.Search, _model.Library, _model.Appearance, _model.Updates, _keys];

    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (!_loading && !_window.IsBusy && !_disposed)
        {
            CancelSuccessStatus();
            CaptureDraft();
        }
    }

    private void SessionChanged(object? sender, EventArgs args) =>
        _window.HasUnsavedChanges = _session.HasChanges;

    private void CaptureDraft()
    {
        var b = _session.Baseline;
        var g = _model.General; var s = _model.Search; var l = _model.Library; var u = _model.Updates;
        var draft = b with
        {
            Hotkey = g.Hotkey.Trim(),
            Startup = b.Startup with { StartWithWindows = g.StartWithWindows },
            Behavior = b.Behavior with { CloseWhenFocusLost = g.CloseWhenFocusLost,
                HideAfterCopy = g.HideAfterCopy, CloseToTray = g.CloseToTray },
            Window = b.Window with { PlacementMode = g.PlacementMode,
                RememberWindowSize = g.RememberWindowSize, CenterOnTrayOpen = g.CenterOnTrayOpen },
            Appearance = b.Appearance with { Theme = _model.Appearance.Theme, DisplayQuality = _model.Appearance.DisplayQuality },
            Search = b.Search with { ResultsPerSearch = s.ResultsPerSearch,
                ContentRating = s.ContentRating,
                DebounceMilliseconds = s.DebounceMilliseconds, AnimatePreviews = s.AnimatePreviews,
                AutoLoadMoreResults = s.AutoLoadMoreResults, ShowTrendingWhenEmpty = s.ShowTrendingWhenEmpty,
                SaveSearchHistory = s.SaveSearchHistory, UseHistorySuggestions = s.UseHistorySuggestions,
                SearchHistoryLimit = s.SearchHistoryLimit },
            Library = b.Library with { RecentLimit = l.RecentLimit, FavoriteLimit = l.FavoriteLimit,
                GifQuality = _model.Appearance.CopyQuality, SaveQuality = _model.Appearance.SaveQuality,
                StoreFavoritesLocally = l.StoreFavoritesLocally, StoreRecentsLocally = l.StoreRecentsLocally,
                CustomStorageRoot = l.CustomStorageRoot },
            Updates = b.Updates with { CheckForUpdates = u.CheckForUpdates,
                CheckFrequency = u.CheckFrequency, Mode = u.Mode },
            Providers = b.Providers with { ActiveProviderId = _keys.ActiveProviderId }
        };
        _session.SetDraft(draft);
        // Runtime preview intentionally leaves credentials, startup, storage and provider selection committed.
        _runtime.ApplySettings(draft with { Providers = b.Providers, Library = b.Library,
            Startup = b.Startup, Hotkey = b.Hotkey, Updates = b.Updates });
    }

    private async Task<bool> ApplyAsync()
    {
        if (_loading || _window.IsBusy) return false;
        CaptureDraft();
        _window.IsBusy = true;
        try
        {
            var result = await _session.ApplyAsync();
            _runtime.ApplySettings(result.EffectiveSettings);
            await _keys.RefreshAsync();
            _window.HasUnsavedChanges = false;
            _window.StatusMessage = "Settings saved.";
            _window.StatusSeverity = InfoBarSeverity.Success;
            _successStatusTimer = new CancellationTokenSource();
            _ = ClearSuccessAfterDelayAsync(_successStatusTimer.Token);
            return true;
        }
        catch (Exception exception)
        {
            _window.StatusMessage = exception.Message;
            _window.StatusSeverity = InfoBarSeverity.Error;
            return false;
        }
        finally { _window.IsBusy = false; }
    }

    private async Task<bool> ResolveCloseAsync()
    {
        if (_window.IsBusy || _dialogOpen) return false;
        if (_loading || !_session.HasChanges) { _session.Close(); return true; }
        _dialogOpen = true;
        ContentDialogResult choice;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = _window.RootElement.XamlRoot,
                RequestedTheme = _window.RootElement.ActualTheme,
                Title = "Apply your changes?",
                Content = "You have changes in Settings that have not been applied.",
                PrimaryButtonText = "Apply and close",
                SecondaryButtonText = "Discard",
                CloseButtonText = "Keep editing",
                DefaultButton = ContentDialogButton.Close
            };
            choice = await dialog.ShowAsync();
        }
        finally { _dialogOpen = false; }
        if (choice == ContentDialogResult.None) return false;
        if (choice == ContentDialogResult.Primary && !await ApplyAsync()) return false;
        if (choice == ContentDialogResult.Secondary) { _session.Discard(); _runtime.ApplySettings(_session.Baseline); }
        else _session.Close();
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelSuccessStatus();
        foreach (INotifyPropertyChanged section in Sections()) section.PropertyChanged -= Changed;
        _session.Changed -= SessionChanged;
        _session.Close();
        _model.Library.PickFolder = null;
        _model.Library.OpenFolder = null;
    }

    private void CancelSuccessStatus()
    {
        _successStatusTimer?.Cancel();
        _successStatusTimer?.Dispose();
        _successStatusTimer = null;
        if (_window.StatusSeverity == InfoBarSeverity.Success)
            _window.StatusMessage = string.Empty;
    }

    private async Task ClearSuccessAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (!_disposed && !token.IsCancellationRequested &&
                    _window.StatusSeverity == InfoBarSeverity.Success)
                    _window.StatusMessage = string.Empty;
            });
        }
        catch (OperationCanceledException) { }
    }

    private sealed class Owner(SettingsWindow window) : IWindowHandleProvider
    {
        public nint GetWindowHandle() => WinRT.Interop.WindowNative.GetWindowHandle(window);
    }
}
