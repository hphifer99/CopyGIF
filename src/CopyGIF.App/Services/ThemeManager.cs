using CopyGIF.Core.Settings;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace CopyGIF.App.Services;

public sealed class ThemeManager :
    IDisposable
{
    private readonly WinUiDispatcher
        _dispatcher;

    private readonly UISettings
        _uiSettings =
            new();

    private readonly AccessibilitySettings
        _accessibilitySettings =
            new();

    private readonly HashSet<FrameworkElement>
        _roots =
            [];

    private AppTheme _selectedTheme =
        AppTheme.System;

    private bool _disposed;

    public ThemeManager(
        WinUiDispatcher dispatcher)
    {
        _dispatcher =
            dispatcher ??
            throw new ArgumentNullException(
                nameof(dispatcher));

        _accessibilitySettings.HighContrastChanged +=
            HandleHighContrastChanged;

        _uiSettings.AdvancedEffectsEnabledChanged +=
            HandleAnimationsEnabledChanged;
    }

    public event EventHandler? ThemeChanged;

    public event EventHandler?
        AnimationsEnabledChanged;

    public event EventHandler?
        HighContrastChanged;

    public AppTheme SelectedTheme =>
        _selectedTheme;

    public bool AnimationsEnabled =>
        _uiSettings.AnimationsEnabled;

    public bool IsHighContrast =>
        _accessibilitySettings.HighContrast;

    public void RegisterRoot(
        FrameworkElement root)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            root);

        EnsureUiThread();

        if (!_roots.Add(
                root))
        {
            return;
        }

        root.ActualThemeChanged +=
            HandleActualThemeChanged;

        root.RequestedTheme =
            GetRequestedTheme();
    }

    public void UnregisterRoot(
        FrameworkElement root)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            root);

        EnsureUiThread();

        if (!_roots.Remove(
                root))
        {
            return;
        }

        root.ActualThemeChanged -=
            HandleActualThemeChanged;
    }

    public Task ApplyThemeAsync(
        AppTheme theme)
    {
        ThrowIfDisposed();

        if (!Enum.IsDefined(
                theme))
        {
            throw new ArgumentOutOfRangeException(
                nameof(theme),
                theme,
                "The application theme is not supported.");
        }

        return _dispatcher.InvokeAsync(
            () =>
            {
                _selectedTheme =
                    theme;

                ApplySelectedThemeCore();

                ThemeChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        _accessibilitySettings.HighContrastChanged -=
            HandleHighContrastChanged;

        _uiSettings.AdvancedEffectsEnabledChanged -=
            HandleAnimationsEnabledChanged;

        foreach (FrameworkElement root in _roots)
        {
            root.ActualThemeChanged -=
                HandleActualThemeChanged;
        }

        _roots.Clear();

        GC.SuppressFinalize(
            this);
    }

    private void ApplySelectedThemeCore()
    {
        ElementTheme requestedTheme =
            GetRequestedTheme();

        foreach (FrameworkElement root in _roots)
        {
            root.RequestedTheme =
                requestedTheme;
        }
    }

    private ElementTheme GetRequestedTheme()
    {
        if (IsHighContrast)
        {
            return ElementTheme.Default;
        }

        return _selectedTheme switch
        {
            AppTheme.System =>
                ElementTheme.Default,

            AppTheme.Light =>
                ElementTheme.Light,

            AppTheme.Dark =>
                ElementTheme.Dark,

            _ =>
                throw new InvalidOperationException(
                    "The selected application theme is not supported.")
        };
    }

    private void HandleActualThemeChanged(
        FrameworkElement _,
        object __)
    {
        ThemeChanged?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void HandleHighContrastChanged(
        AccessibilitySettings _,
        object __)
    {
        _dispatcher.TryEnqueue(
            () =>
            {
                ApplySelectedThemeCore();

                HighContrastChanged?.Invoke(
                    this,
                    EventArgs.Empty);

                ThemeChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            });
    }

    private void HandleAnimationsEnabledChanged(
        UISettings _,
        object __)
    {
        _dispatcher.TryEnqueue(
            () =>
            {
                AnimationsEnabledChanged?.Invoke(
                    this,
                    EventArgs.Empty);
            });
    }

    private void EnsureUiThread()
    {
        if (!_dispatcher.HasThreadAccess)
        {
            throw new InvalidOperationException(
                "Theme roots must be registered and removed on the UI thread.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}
