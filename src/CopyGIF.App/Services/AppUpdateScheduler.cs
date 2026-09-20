using CopyGIF.Application.Settings;
using CopyGIF.Application.Updates;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Updates;
using Microsoft.UI.Xaml;

namespace CopyGIF.App.Services;

public sealed class AppUpdateScheduler : IAsyncDisposable
{
    private readonly IUpdateCoordinator _updates;
    private readonly EffectiveSettings _effective;
    private readonly ITrayService _tray;
    private readonly WinUiDispatcher _dispatcher;
    private readonly UpdateViewModel _viewModel;
    private readonly WindowManager _windowManager;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _sync = new();
    private readonly SemaphoreSlim _wake = new(0, 1);
    private CancellationTokenSource? _current;
    private Task? _loop;
    private DateTimeOffset _retryAfter;
    // Tray notifications already shown this session, keyed by action and version,
    // so a package that stays pending does not raise the same toast every check.
    private readonly HashSet<string> _notified = new(StringComparer.Ordinal);
    // Versions already offered to the user, or already handed to the installer, in this
    // session. This keeps a declined prompt or a declined UAC dialog from repeating.
    private readonly HashSet<string> _handledVersions = new(StringComparer.OrdinalIgnoreCase);

    public AppUpdateScheduler(IUpdateCoordinator updates, EffectiveSettings effective, ITrayService tray,
        WinUiDispatcher dispatcher, UpdateViewModel viewModel, WindowManager windowManager)
    {
        _updates = updates; _effective = effective; _tray = tray;
        _dispatcher = dispatcher; _viewModel = viewModel; _windowManager = windowManager;
        effective.Changed += SettingsChanged;
        viewModel.ApplicationExitRequested += ApplicationExitRequested;
    }

    public void Start(string version)
    {
        if (_loop is not null) return;
        _viewModel.Initialize(version);
        _loop = Task.Run(() => RunAsync(version));
    }

    private void SettingsChanged(object? sender, EventArgs args)
    {
        lock (_sync)
        {
            _current?.Cancel();
            if (_wake.CurrentCount == 0) _wake.Release();
        }
    }

    // The installer has been handed off with the restart option. CopyGIF must close so the
    // installer can replace its files; the installer package starts CopyGIF again afterwards.
    private void ApplicationExitRequested(object? sender, EventArgs args) => _ = ExitForUpdateAsync();

    private async Task ExitForUpdateAsync()
    {
        try
        {
            Func<Task> exit = () => _windowManager.ExitAsync();
            await _dispatcher.InvokeAsync(exit);
        }
        catch (Exception exception)
        {
            RepairDiagnostics.Record("update-exit", "local", exception.GetType().Name);
        }
    }

    private async Task RunAsync(string version)
    {
        try
        {
            while (!_lifetime.IsCancellationRequested)
            {
                TimeSpan nextCheck = TimeSpan.FromMinutes(15);
                using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
                lock (_sync) _current = operation;
                try
                {
                    if (!_effective.SuppressAutomaticUpdates && !_viewModel.IsBusy && DateTimeOffset.UtcNow >= _retryAfter)
                    {
                        AutomaticUpdateResult result = await _updates.RunAutomaticAsync(version, cancellationToken: operation.Token);
                        if (result.Check.Status is UpdateCheckStatus.FeedUnavailable or UpdateCheckStatus.UnsupportedInstallation ||
                            result.Action == AutomaticUpdateAction.RetryLater)
                            _retryAfter = DateTimeOffset.UtcNow.AddHours(1);
                        if (result.Check.Status is UpdateCheckStatus.ManagedByStore or
                            UpdateCheckStatus.UnsupportedInstallation or UpdateCheckStatus.Disabled)
                            nextCheck = TimeSpan.FromDays(1);
                        else if (result.Check.Status == UpdateCheckStatus.FeedUnavailable ||
                                 result.Action == AutomaticUpdateAction.RetryLater)
                            nextCheck = TimeSpan.FromHours(1);
                        if (result.Check.Status != UpdateCheckStatus.NotDue)
                            await _dispatcher.InvokeAsync(() => _viewModel.AcceptAutomaticResult(result));
                        bool handled = await HandlePreparedUpdateAsync(result, operation.Token);
                        // RetryLater is silent: the certificate servers were unreachable, which
                        // says nothing about the update itself. The next attempt is in an hour.
                        if (result.Action is not (AutomaticUpdateAction.None or AutomaticUpdateAction.RetryLater) &&
                            !handled && ShouldNotify(result))
                            await _tray.ShowNotificationAsync("CopyGIF update",
                                result.Action == AutomaticUpdateAction.Installed ? "The verified update installer has started." :
                                result.Action == AutomaticUpdateAction.VerificationFailed ? "Update verification failed. Open Settings for details." :
                                "An update is available. Open Settings > Updates for details.", _lifetime.Token);
                    }
                }
                catch (OperationCanceledException) when (operation.IsCancellationRequested) { }
                catch (Exception exception)
                {
                    _retryAfter = DateTimeOffset.UtcNow.AddHours(1);
                    nextCheck = TimeSpan.FromHours(1);
                    RepairDiagnostics.Record("automatic-update", "github", exception.GetType().Name);
                }
                finally { lock (_sync) if (ReferenceEquals(_current, operation)) _current = null; }
                await _wake.WaitAsync(nextCheck, _lifetime.Token);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    // Returns true when the prepared update was handed to the user or the installer, so the
    // generic "an update is available" toast is not needed.
    private async Task<bool> HandlePreparedUpdateAsync(AutomaticUpdateResult result, CancellationToken cancellationToken)
    {
        if (result.Action != AutomaticUpdateAction.Prompt ||
            result.Preparation?.Package is not { } package)
            return false;

        string updateVersion = package.Manifest.Version;
        UpdateFollowUp followUp = UpdateHandoffPolicy.Decide(
            result, IsApplicationInForeground(), _handledVersions.Contains(updateVersion));

        switch (followUp)
        {
            case UpdateFollowUp.InstallInBackground:
                // At most one silent attempt per version per session, even if the UAC prompt
                // is declined, so the user is not prompted again every check.
                _handledVersions.Add(updateVersion);
                Func<Task> installInBackground = () => _viewModel.InstallInBackgroundAsync(cancellationToken);
                await _dispatcher.InvokeAsync(installInBackground);
                return true;

            case UpdateFollowUp.PromptUser:
                _handledVersions.Add(updateVersion);
                // The dialog can stay open for as long as the user likes. It must never hold
                // the update loop, or shutdown would wait for it, so it runs on its own.
                // "Not now" installs the update at the next start, but only for a per-user
                // installation (no administrator prompt), so the dialog text depends on it.
                _ = PromptAndHandleAsync(
                    package,
                    result.Check.Installation.Scope == InstallScope.CurrentUser);
                return true;

            default:
                // Nothing to do. If this version was already handled, the user was already told.
                return _handledVersions.Contains(updateVersion);
        }
    }

    // Foreground means the app or a settings window is on screen. A hidden tray app is background.
    private bool IsApplicationInForeground() =>
        _windowManager.IsPickerVisible || _windowManager.SettingsWindow is not null;

    private async Task PromptAndHandleAsync(DownloadedUpdatePackage package, bool installsAtNextStart)
    {
        string updateVersion = package.Manifest.Version;
        try
        {
            UpdatePromptChoice? choice = await PromptOnUiThreadAsync(updateVersion, installsAtNextStart);
            switch (choice)
            {
                case UpdatePromptChoice.InstallNow:
                    Func<Task> install = () => _viewModel.InstallCommand.ExecuteAsync(null);
                    await _dispatcher.InvokeAsync(install);
                    break;
                case UpdatePromptChoice.SkipVersion:
                    await _updates.SkipVersionAsync(updateVersion, _lifetime.Token);
                    break;
                case UpdatePromptChoice.RemindLater:
                    // "Not now": remember the verified package so the next start installs it.
                    // Nothing is remembered when that is not possible for this installation.
                    await _updates.DeferInstallToNextLaunchAsync(package, _lifetime.Token);
                    break;
                default:
                    // No window could host the dialog. Fall back to the tray notification.
                    await _tray.ShowNotificationAsync("CopyGIF update",
                        "An update is available. Open Settings > Updates for details.", _lifetime.Token);
                    break;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            RepairDiagnostics.Record("update-prompt", "local", exception.GetType().Name);
        }
    }

    private async Task<UpdatePromptChoice?> PromptOnUiThreadAsync(string updateVersion, bool installsAtNextStart)
    {
        UpdatePromptChoice? choice = null;
        try
        {
            Func<Task> show = async () =>
            {
                FrameworkElement? host = _windowManager.IsPickerVisible
                    ? _windowManager.MainWindow?.RootElement
                    : _windowManager.SettingsWindow?.RootElement;
                if (host?.XamlRoot is null) return;
                choice = await UpdateInstallPrompt.ShowAsync(host, updateVersion, installsAtNextStart);
            };
            await _dispatcher.InvokeAsync(show);
        }
        catch (Exception exception)
        {
            // For example another dialog is already open in that window. A failed dialog is not
            // user consent to install at next launch, so fall back to a notification instead.
            RepairDiagnostics.Record("update-prompt", "local", exception.GetType().Name);
            return null;
        }
        return choice;
    }

    private bool ShouldNotify(AutomaticUpdateResult result)
    {
        string version = result.Check.Candidate?.AvailableVersion ?? string.Empty;
        return _notified.Add($"{result.Action}:{version}");
    }

    public async ValueTask DisposeAsync()
    {
        _effective.Changed -= SettingsChanged;
        _viewModel.ApplicationExitRequested -= ApplicationExitRequested;
        await _lifetime.CancelAsync();
        if (_loop is not null) await _loop;
        _wake.Dispose();
        _lifetime.Dispose();
    }
}
