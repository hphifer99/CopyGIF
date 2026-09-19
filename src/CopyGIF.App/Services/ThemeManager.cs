using System.Diagnostics;
using System.Runtime.InteropServices;
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

    private bool
        _systemEventSubscriptionAttempted;

    private bool
        _highContrastChangedSubscribed;

    private bool
        _animationsEnabledChangedSubscribed;

    private bool _disposed;

    private DispatcherTimer? _animationSettingsTimer;
    private bool _lastAnimationsEnabled;

    public ThemeManager(
        WinUiDispatcher dispatcher)
    {
        _dispatcher =
            dispatcher ??
            throw new ArgumentNullException(
                nameof(dispatcher));
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

    public void SetPickerVisible(bool visible)
    {
        ThrowIfDisposed();
        EnsureUiThread();
        if (visible)
        {
            CheckAnimationSettings();
            _animationSettingsTimer?.Start();
        }
        else _animationSettingsTimer?.Stop();
    }

    public void RegisterRoot(
        FrameworkElement root)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            root);

        EnsureUiThread();
        EnsureSystemEventSubscriptions();

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

        UnsubscribeFromSystemEvents();

        foreach (FrameworkElement root in _roots)
        {
            root.ActualThemeChanged -=
                HandleActualThemeChanged;
        }

        _roots.Clear();

        GC.SuppressFinalize(
            this);
    }

    private void EnsureSystemEventSubscriptions()
    {
        if (_systemEventSubscriptionAttempted)
        {
            return;
        }

        _systemEventSubscriptionAttempted =
            true;

        // AnimationsEnabledChanged requires Windows 10 version 2004. Keep
        // the existing 1809 minimum and read the supported property on the
        // UI thread, including when an animation preference changes alone.
        _lastAnimationsEnabled = _uiSettings.AnimationsEnabled;
        _animationSettingsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _animationSettingsTimer.Tick += HandleAnimationSettingsTick;

        try
        {
            _accessibilitySettings.HighContrastChanged +=
                HandleHighContrastChanged;

            _highContrastChangedSubscribed =
                true;
        }
        catch (COMException exception)
        {
            Debug.WriteLine(
                $"CopyGIF could not monitor high-contrast changes: {exception}");
        }

        try
        {
            _uiSettings.AdvancedEffectsEnabledChanged +=
                HandleAnimationsEnabledChanged;

            _animationsEnabledChangedSubscribed =
                true;
        }
        catch (COMException exception)
        {
            Debug.WriteLine(
                $"CopyGIF could not monitor animation-setting changes: {exception}");
        }
    }

    private void UnsubscribeFromSystemEvents()
    {
        if (_animationSettingsTimer is not null)
        {
            DispatcherTimer timer = _animationSettingsTimer;
            _animationSettingsTimer = null;

            void StopTimer()
            {
                timer.Stop();
                timer.Tick -= HandleAnimationSettingsTick;
            }

            if (_dispatcher.HasThreadAccess)
            {
                StopTimer();
            }
            else
            {
                _dispatcher.TryEnqueue(StopTimer);
            }
        }

        if (_highContrastChangedSubscribed)
        {
            try
            {
                _accessibilitySettings.HighContrastChanged -=
                    HandleHighContrastChanged;
            }
            catch (COMException exception)
            {
                Debug.WriteLine(
                    $"CopyGIF could not stop monitoring high-contrast changes: {exception}");
            }

            _highContrastChangedSubscribed =
                false;
        }

        if (_animationsEnabledChangedSubscribed)
        {
            try
            {
                _uiSettings.AdvancedEffectsEnabledChanged -=
                    HandleAnimationsEnabledChanged;
            }
            catch (COMException exception)
            {
                Debug.WriteLine(
                    $"CopyGIF could not stop monitoring animation-setting changes: {exception}");
            }

            _animationsEnabledChangedSubscribed =
                false;
        }
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
            CheckAnimationSettings);
    }

    private void HandleAnimationSettingsTick(object? sender, object eventArgs)
    {
        CheckAnimationSettings();
    }

    private void CheckAnimationSettings()
    {
        if (_disposed)
        {
            return;
        }

        bool enabled = _uiSettings.AnimationsEnabled;
        if (_lastAnimationsEnabled == enabled)
        {
            return;
        }

        _lastAnimationsEnabled = enabled;
        AnimationsEnabledChanged?.Invoke(this, EventArgs.Empty);
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
