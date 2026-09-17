using CopyGIF.Application.Onboarding;
using CopyGIF.Application.Settings;
using CopyGIF.Application.Startup;
using CopyGIF.App.Views;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using XamlApplication = Microsoft.UI.Xaml.Application;

namespace CopyGIF.App.Services;

public sealed class WindowManager :
    IAsyncDisposable
{
    private const int SettingsWindowWidth =
        900;

    private const int SettingsWindowHeight =
        720;

    private const int OnboardingWindowWidth =
        640;

    private const int OnboardingWindowHeight =
        520;

    private readonly Func<MainWindow>
        _mainWindowFactory;

    private readonly Func<SettingsWindow>
        _settingsWindowFactory;

    private readonly Func<OnboardingWindow>
        _onboardingWindowFactory;

    private readonly IApplicationStartupCoordinator
        _startupCoordinator;

    private readonly IWindowPlacementService
        _placementService;

    private readonly ISettingsCoordinator
        _settingsCoordinator;

    private readonly WinUiDispatcher
        _dispatcher;

    private readonly ThemeManager
        _themeManager;

    private readonly SemaphoreSlim _operationGate =
        new(
            initialCount: 1,
            maxCount: 1);

    private readonly CancellationTokenSource
        _lifetimeCancellation =
            new();

    private MainWindow? _mainWindow;

    private SettingsWindow? _settingsWindow;

    private OnboardingWindow? _onboardingWindow;

    private KeyboardAccelerator? _escapeAccelerator;

    private AppSettings _settings =
        new();

    private WindowPlacementResult? _lastPlacement;

    private bool _onboardingRequired;

    private bool _initialized;

    private bool _isPickerVisible;

    private bool _isExiting;

    private int _disposeState;
    private EffectiveSettings? _effectiveSettings;
    private long _activationGeneration;
    private int _focusDeferrals;
    private bool _closingSettings;
    public Func<Task>? ShutdownAsync { get; set; }

    public void AttachEffectiveSettings(EffectiveSettings effective)
    {
        _effectiveSettings = effective;
        effective.Changed += HandleEffectiveSettingsChanged;
    }

    private void HandleEffectiveSettingsChanged(object? sender, EventArgs args)
    {
        if (!_initialized || _isExiting) return;
        QueueOperation(async token =>
        {
            _settings = await _effectiveSettings!.LoadAsync(token);
            await _themeManager.ApplyThemeAsync(_settings.Appearance.Theme);
        });
    }


    public WindowManager(
        Func<MainWindow> mainWindowFactory,
        Func<SettingsWindow> settingsWindowFactory,
        Func<OnboardingWindow> onboardingWindowFactory,
        IApplicationStartupCoordinator startupCoordinator,
        IWindowPlacementService placementService,
        ISettingsCoordinator settingsCoordinator,
        WinUiDispatcher dispatcher,
        ThemeManager themeManager)
    {
        _mainWindowFactory =
            mainWindowFactory ??
            throw new ArgumentNullException(
                nameof(mainWindowFactory));

        _settingsWindowFactory =
            settingsWindowFactory ??
            throw new ArgumentNullException(
                nameof(settingsWindowFactory));

        _onboardingWindowFactory =
            onboardingWindowFactory ??
            throw new ArgumentNullException(
                nameof(onboardingWindowFactory));

        _startupCoordinator =
            startupCoordinator ??
            throw new ArgumentNullException(
                nameof(startupCoordinator));

        _placementService =
            placementService ??
            throw new ArgumentNullException(
                nameof(placementService));

        _settingsCoordinator =
            settingsCoordinator ??
            throw new ArgumentNullException(
                nameof(settingsCoordinator));

        _dispatcher =
            dispatcher ??
            throw new ArgumentNullException(
                nameof(dispatcher));

        _themeManager =
            themeManager ??
            throw new ArgumentNullException(
                nameof(themeManager));

        _startupCoordinator.ActivationRequested +=
            HandleActivationRequested;

        _startupCoordinator.HotkeyActivated +=
            HandleHotkeyActivated;

        _startupCoordinator.OpenRequested +=
            HandleOpenRequested;

        _startupCoordinator.SettingsRequested +=
            HandleSettingsRequested;

        _startupCoordinator.ExitRequested +=
            HandleExitRequested;
    }

    public event EventHandler?
        OperationFailed;

    public bool IsInitialized =>
        _initialized;

    public bool IsPickerVisible =>
        _isPickerVisible;

    public Exception? LastOperationException
    {
        get;
        private set;
    }

    public MainWindow? MainWindow =>
        _mainWindow;

    public SettingsWindow? SettingsWindow =>
        _settingsWindow;

    public OnboardingWindow? OnboardingWindow =>
        _onboardingWindow;

    public Task InitializeAsync(
        AppSettings settings,
        OnboardingState onboarding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        ArgumentNullException.ThrowIfNull(
            onboarding);

        return ExecuteSerializedAsync(
            async operationToken =>
            {
                if (_initialized)
                {
                    throw new InvalidOperationException(
                        "Window management has already been initialized.");
                }

                _settings =
                    settings;

                _onboardingRequired =
                    onboarding.IsRequired;

                await _themeManager
                    .ApplyThemeAsync(
                        settings.Appearance.Theme)
                    .ConfigureAwait(true);

                if (_onboardingRequired)
                {
                    await ShowOnboardingCoreAsync(
                            operationToken)
                        .ConfigureAwait(true);
                }
                else
                {
                    await ShowPickerCoreAsync(
                            operationToken)
                        .ConfigureAwait(true);
                }

                _initialized =
                    true;
            },
            cancellationToken);
    }

    public Task ApplySettingsAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        return ExecuteSerializedAsync(
            async operationToken =>
            {
                _ = operationToken;

                EnsureInitialized();

                _settings =
                    settings;

                await _themeManager
                    .ApplyThemeAsync(
                        settings.Appearance.Theme)
                    .ConfigureAwait(true);
            },
            cancellationToken);
    }

    public Task ShowPickerAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteSerializedAsync(
            async operationToken =>
            {
                EnsureInitialized();

                await ShowPickerCoreAsync(
                        operationToken)
                    .ConfigureAwait(true);
            },
            cancellationToken);
    }

    public Task HidePickerAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteSerializedAsync(
            async operationToken =>
            {
                EnsureInitialized();

                await HidePickerCoreAsync(
                        persistBounds: true,
                        operationToken)
                    .ConfigureAwait(true);
            },
            cancellationToken);
    }

    public Task HandleCopyCompletedAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteSerializedAsync(
            async operationToken =>
            {
                EnsureInitialized();

                if (!_settings.Behavior.HideAfterCopy)
                {
                    return;
                }

                await HidePickerCoreAsync(
                        persistBounds: true,
                        operationToken)
                    .ConfigureAwait(true);
            },
            cancellationToken);
    }

    public Task ShowSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteSerializedAsync(
            async operationToken =>
            {
                EnsureInitialized();

                await ShowSettingsCoreAsync(
                        operationToken)
                    .ConfigureAwait(true);
            },
            cancellationToken);
    }

    public Task ShowOnboardingAsync(
        CancellationToken cancellationToken = default)
    {
        return ExecuteSerializedAsync(
            async operationToken =>
            {
                EnsureInitialized();

                _onboardingRequired =
                    true;

                await ShowOnboardingCoreAsync(
                        operationToken)
                    .ConfigureAwait(true);
            },
            cancellationToken);
    }

    public Task CompleteOnboardingAsync(
    CancellationToken cancellationToken = default)
    {
        return ExecuteSerializedAsync(
            async operationToken =>
            {
                EnsureInitialized();

                _onboardingRequired =
                    false;

                await ShowPickerCoreAsync(
                        operationToken)
                    .ConfigureAwait(true);

                CloseOnboardingWindow();
            },
            cancellationToken);
    }

    public Task ExitAsync(CancellationToken cancellationToken = default) =>
        ExecuteSerializedAsync(ExitCoreAsync, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposeState,
                1) != 0)
        {
            return;
        }

        UnsubscribeFromStartupEvents();
        if (_effectiveSettings is not null) _effectiveSettings.Changed -= HandleEffectiveSettingsChanged;

        await _lifetimeCancellation
            .CancelAsync()
            .ConfigureAwait(false);

        await _operationGate
            .WaitAsync()
            .ConfigureAwait(false);

        try
        {
            if (_dispatcher.IsInitialized)
            {
                await _dispatcher
                    .InvokeAsync(
                        CloseAllWindows)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            _operationGate.Release();
            _operationGate.Dispose();
            _lifetimeCancellation.Dispose();

            Interlocked.Exchange(
                ref _disposeState,
                2);
        }
    }

    private async Task ExecuteSerializedAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            operation);

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCancellation.Token);

        await _operationGate
            .WaitAsync(
                linkedCancellation.Token)
            .ConfigureAwait(false);

        try
        {
            Func<Task> dispatcherOperation =
                () =>
                    RunTransitionAsync(operation, linkedCancellation.Token);

            await _dispatcher
                .InvokeAsync(
                    dispatcherOperation)
                .ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task RunTransitionAsync(Func<CancellationToken, Task> operation, CancellationToken token)
    {
        _focusDeferrals++;
        try { await operation(token); }
        finally { _focusDeferrals--; }
    }

    private async Task ShowPickerCoreAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _activationGeneration);

        if (_onboardingRequired)
        {
            await ShowOnboardingCoreAsync(
                    cancellationToken)
                .ConfigureAwait(true);

            return;
        }

        _settings = _effectiveSettings is null
            ? await _settingsCoordinator.LoadAsync(cancellationToken)
            : await _effectiveSettings.LoadAsync(cancellationToken);
        _settingsWindow?.AppWindow.Hide();
        await _themeManager.ApplyThemeAsync(_settings.Appearance.Theme)
            .ConfigureAwait(true);

        MainWindow window =
            EnsureMainWindow();

        WindowPlacementResult placement =
            await _placementService
                .CalculateAsync(
                    _settings.Window,
                    cancellationToken)
                .ConfigureAwait(true);

        window.AppWindow.MoveAndResize(
            CreateRectangle(
                placement));

        _lastPlacement =
            placement;

        NativeWindowActivation.RestoreAndActivate(window);

        _isPickerVisible =
            true;

        if (!window.DispatcherQueue.TryEnqueue(
                window.FocusSearch))
        {
            window.FocusSearch();
        }
    }

    private async Task HidePickerCoreAsync(
        bool persistBounds,
        CancellationToken cancellationToken)
    {
        if (_mainWindow is null ||
            !_isPickerVisible)
        {
            return;
        }

        Exception? persistenceFailure =
            null;

        if (persistBounds)
        {
            try
            {
                await PersistPickerBoundsAsync(
                        cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                persistenceFailure =
                    exception;
            }
        }

        _isPickerVisible =
            false;

        _mainWindow.AppWindow.Hide();

        if (persistenceFailure is not null)
        {
            ReportOperationFailure(persistenceFailure);
        }
    }

    private async Task ShowSettingsCoreAsync(
        CancellationToken cancellationToken)
    {
        await HidePickerCoreAsync(
                persistBounds: true,
                cancellationToken)
            .ConfigureAwait(true);

        Interlocked.Increment(ref _activationGeneration);
        SettingsWindow window =
            EnsureSettingsWindow();

        WindowPlacementResult placement = await _placementService
            .CalculateAsync(
                new WindowSettings
                {
                    PlacementMode = WindowPlacementMode.Center,
                    Width = SettingsWindowWidth,
                    Height = SettingsWindowHeight
                },
                cancellationToken)
            .ConfigureAwait(true);

        window.AppWindow.MoveAndResize(CreateRectangle(placement));
        NativeWindowActivation.RestoreAndActivate(window);
    }

    private async Task ShowOnboardingCoreAsync(
        CancellationToken cancellationToken)
    {
        await HidePickerCoreAsync(
                persistBounds: true,
                cancellationToken)
            .ConfigureAwait(true);

        OnboardingWindow window =
            EnsureOnboardingWindow();

        WindowPlacementResult placement = await _placementService
            .CalculateAsync(
                new WindowSettings
                {
                    PlacementMode = WindowPlacementMode.Center,
                    Width = OnboardingWindowWidth,
                    Height = OnboardingWindowHeight
                },
                cancellationToken)
            .ConfigureAwait(true);

        window.AppWindow.MoveAndResize(CreateRectangle(placement));
        NativeWindowActivation.RestoreAndActivate(window);
    }

    private async Task PersistPickerBoundsAsync(
        CancellationToken cancellationToken)
    {
        if (_mainWindow is null)
        {
            return;
        }

        AppSettings latestSettings = await _settingsCoordinator
            .LoadAsync(cancellationToken).ConfigureAwait(true);
        WindowSettings current = latestSettings.Window;

        bool savePosition =
            current.PlacementMode ==
            WindowPlacementMode.Remember;

        if (!savePosition &&
            !current.RememberWindowSize)
        {
            return;
        }

        AppWindow appWindow =
            _mainWindow.AppWindow;

        PointInt32 position =
            appWindow.Position;

        SizeInt32 size =
            appWindow.Size;

        double rasterizationScale =
            GetRasterizationScale(
                _mainWindow.RootElement);

        bool stayedAtCalculatedPosition =
            IsAtCalculatedPosition(
                position,
                _lastPlacement);

        WindowSettings updatedWindow =
            current with
            {
                Width =
                    current.RememberWindowSize
                        ? size.Width /
                            rasterizationScale
                        : current.Width,
                Height =
                    current.RememberWindowSize
                        ? size.Height /
                            rasterizationScale
                        : current.Height,
                Left =
                    savePosition
                        ? position.X
                        : current.Left,
                Top =
                    savePosition
                        ? position.Y
                        : current.Top,
                LastMonitorId =
                    savePosition
                        ? stayedAtCalculatedPosition
                            ? _lastPlacement?.MonitorId
                            : null
                        : current.LastMonitorId
            };

        if (updatedWindow == current)
        {
            return;
        }

        AppSettings proposedSettings =
            latestSettings with
            {
                Window =
                    updatedWindow
            };

        SettingsSaveResult saveResult =
            await _settingsCoordinator
                .UpdateAsync(latest => latest with { Window = latest.Window with
                    {
                        Width = latest.Window.RememberWindowSize ? updatedWindow.Width : latest.Window.Width,
                        Height = latest.Window.RememberWindowSize ? updatedWindow.Height : latest.Window.Height,
                        Left = latest.Window.PlacementMode == WindowPlacementMode.Remember ? updatedWindow.Left : latest.Window.Left,
                        Top = latest.Window.PlacementMode == WindowPlacementMode.Remember ? updatedWindow.Top : latest.Window.Top,
                        LastMonitorId = latest.Window.PlacementMode == WindowPlacementMode.Remember ? updatedWindow.LastMonitorId : latest.Window.LastMonitorId
                    } }, cancellationToken)
                .ConfigureAwait(true);

        _settings = _effectiveSettings is null ? saveResult.EffectiveSettings
            : await _effectiveSettings.LoadAsync(cancellationToken);

        if (!saveResult.Succeeded)
        {
            throw new InvalidOperationException(
                saveResult.ErrorMessage ??
                "The current window position could not be saved.");
        }
    }

    private MainWindow EnsureMainWindow()
    {
        if (_mainWindow is not null)
        {
            return _mainWindow;
        }

        MainWindow window =
            _mainWindowFactory() ??
            throw new InvalidOperationException(
                "The main window factory returned null.");

        _mainWindow =
            window;

        _themeManager.RegisterRoot(
            window.RootElement);

        window.SettingsRequested +=
            HandleMainWindowSettingsRequested;

        window.Activated +=
            HandleMainWindowActivated;

        window.AppWindow.Closing += HandleMainWindowClosing;

        _escapeAccelerator =
            new KeyboardAccelerator
            {
                Key =
                    VirtualKey.Escape
            };

        _escapeAccelerator.Invoked +=
            HandleEscapeInvoked;

        window.RootElement.KeyboardAccelerators.Add(
            _escapeAccelerator);

        return window;
    }

    private SettingsWindow EnsureSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            return _settingsWindow;
        }

        SettingsWindow window =
            _settingsWindowFactory() ??
            throw new InvalidOperationException(
                "The settings window factory returned null.");

        _settingsWindow =
            window;

        _themeManager.RegisterRoot(
            window.RootElement);

        window.Closed += HandleSettingsWindowClosed;
        window.AppWindow.Closing += HandleSettingsWindowClosing;
        window.Activated += HandleMainWindowActivated;

        return window;
    }

    private OnboardingWindow EnsureOnboardingWindow()
    {
        if (_onboardingWindow is not null)
        {
            return _onboardingWindow;
        }

        OnboardingWindow window =
            _onboardingWindowFactory() ??
            throw new InvalidOperationException(
                "The onboarding window factory returned null.");

        _onboardingWindow =
            window;

        _themeManager.RegisterRoot(
            window.RootElement);

        window.Closed +=
            HandleOnboardingWindowClosed;

        return window;
    }

    private void CloseOnboardingWindow()
    {
        if (_onboardingWindow is null)
        {
            return;
        }

        OnboardingWindow window =
            _onboardingWindow;

        window.Closed -=
            HandleOnboardingWindowClosed;

        _themeManager.UnregisterRoot(
            window.RootElement);

        _onboardingWindow =
            null;

        window.Close();
    }

    private void CloseAllWindows()
    {
        _isExiting =
            true;

        _isPickerVisible =
            false;

        CloseSettingsWindow();
        CloseOnboardingWindow();

        if (_mainWindow is null)
        {
            return;
        }

        MainWindow window =
            _mainWindow;

        window.SettingsRequested -=
            HandleMainWindowSettingsRequested;

        window.Activated -=
            HandleMainWindowActivated;

        window.AppWindow.Closing -= HandleMainWindowClosing;

        if (_escapeAccelerator is not null)
        {
            _escapeAccelerator.Invoked -=
                HandleEscapeInvoked;

            window.RootElement.KeyboardAccelerators.Remove(
                _escapeAccelerator);

            _escapeAccelerator =
                null;
        }

        _themeManager.UnregisterRoot(
            window.RootElement);

        _mainWindow =
            null;

        window.Close();
    }

    private void CloseSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            return;
        }

        SettingsWindow window =
            _settingsWindow;

        window.Closed -= HandleSettingsWindowClosed;
        window.AppWindow.Closing -= HandleSettingsWindowClosing;
        window.Activated -= HandleMainWindowActivated;

        _themeManager.UnregisterRoot(
            window.RootElement);

        _settingsWindow =
            null;

        _closingSettings = true;
        try { window.Close(); }
        finally { _closingSettings = false; }
    }

    private void HandleActivationRequested(
        object? sender,
        ActivationRequestedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        QueueOperation(
            ShowPickerCoreAsync);
    }

    private void HandleHotkeyActivated(
        object? sender,
        EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        QueueOperation(
            ShowPickerCoreAsync);
    }

    private void HandleOpenRequested(object? sender, EventArgs eventArgs)
    {
        Task<WindowPlacementResult>? captured = _settings.Window.CenterOnTrayOpen
            ? _placementService.CalculateAsync(_settings.Window with { PlacementMode = WindowPlacementMode.Center }) : null;
        QueueOperation(async token =>
        {
            await ShowPickerCoreAsync(token);
            if (_mainWindow is null || captured is null || !_settings.Window.CenterOnTrayOpen) return;
            var placement = await captured;
            _mainWindow.AppWindow.MoveAndResize(CreateRectangle(placement));
            _lastPlacement = placement;
        });
    }

    private void HandleSettingsRequested(
        object? sender,
        EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        QueueOperation(
            ShowSettingsCoreAsync);
    }

    private void HandleExitRequested(object? sender, EventArgs eventArgs) => QueueOperation(ExitCoreAsync);

    private void HandleMainWindowSettingsRequested(
        object? sender,
        EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;

        QueueOperation(
            ShowSettingsCoreAsync);
    }

    private void HandleMainWindowActivated(object sender, WindowActivatedEventArgs eventArgs)
    {
        if (eventArgs.WindowActivationState != WindowActivationState.Deactivated ||
            _focusDeferrals != 0 || !_settings.Behavior.CloseWhenFocusLost || _isExiting)
            return;
        long generation = Volatile.Read(ref _activationGeneration);
        _ = HideAfterFocusLeavesAsync(generation);
    }

    private async Task HideAfterFocusLeavesAsync(long generation)
    {
        try
        {
            await Task.Delay(100, _lifetimeCancellation.Token);
            await ExecuteSerializedAsync(async token =>
            {
                if (generation != Volatile.Read(ref _activationGeneration) ||
                    NativeWindowActivation.IsProcessForeground() ||
                    !_settings.Behavior.CloseWhenFocusLost ||
                    _settingsWindow?.IsInteractionProtected?.Invoke() == true) return;
                _settingsWindow?.AppWindow.Hide();
                await HidePickerCoreAsync(true, token);
            }, _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception exception) { ReportOperationFailure(exception); }
    }

    private void HandleMainWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting) return;
        args.Cancel = true;
        QueueOperation(_settings.Behavior.CloseToTray
            ? token => HidePickerCoreAsync(true, token)
            : ExitCoreAsync);
    }

    private void HandleSettingsWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting || _closingSettings) return;
        args.Cancel = true;
        QueueOperation(async token =>
        {
            if (!await ResolveSettingsCloseAsync()) return;
            CloseSettingsWindow();
            await ShowPickerCoreAsync(token);
        });
    }

    private async Task<bool> ResolveSettingsCloseAsync()
    {
        if (_settingsWindow is null) return true;
        NativeWindowActivation.RestoreAndActivate(_settingsWindow);
        return _settingsWindow.CanCloseAsync is null || await _settingsWindow.CanCloseAsync();
    }

    private async Task ExitCoreAsync(CancellationToken token)
    {
        if (!await ResolveSettingsCloseAsync()) return;
        if (_isPickerVisible)
        {
            try { await PersistPickerBoundsAsync(token); }
            catch (Exception exception) { ReportOperationFailure(exception); }
        }
        CloseAllWindows();
        // Dispose the host after releasing this manager's operation gate.
        if (ShutdownAsync is not null)
            _ = ShutdownAfterTransitionAsync();
        else XamlApplication.Current.Exit();
    }

    private async Task ShutdownAfterTransitionAsync()
    {
        await Task.Yield();
        try { await ShutdownAsync!(); }
        catch (Exception exception) { ReportOperationFailure(exception); XamlApplication.Current.Exit(); }
    }

    private void HandleSettingsWindowClosed(
        object sender,
        WindowEventArgs eventArgs)
    {
        _ = eventArgs;

        if (!ReferenceEquals(
                sender,
                _settingsWindow) ||
            _settingsWindow is null)
        {
            return;
        }

        _themeManager.UnregisterRoot(
            _settingsWindow.RootElement);

        _settingsWindow =
            null;
    }

    private void HandleOnboardingWindowClosed(
        object sender,
        WindowEventArgs eventArgs)
    {
        _ = eventArgs;

        if (!ReferenceEquals(
                sender,
                _onboardingWindow) ||
            _onboardingWindow is null)
        {
            return;
        }

        _themeManager.UnregisterRoot(
            _onboardingWindow.RootElement);

        _onboardingWindow =
            null;
    }

    private void HandleEscapeInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs
            eventArgs)
    {
        _ = sender;

        eventArgs.Handled =
            true;

        QueueOperation(
            cancellationToken =>
                HidePickerCoreAsync(
                    persistBounds: true,
                    cancellationToken));
    }

    private void QueueOperation(
        Func<CancellationToken, Task> operation)
    {
        _ = ObserveQueuedOperationAsync(
            operation);
    }

    private async Task ObserveQueuedOperationAsync(
        Func<CancellationToken, Task> operation)
    {
        try
        {
            await ExecuteSerializedAsync(
                    async cancellationToken =>
                    {
                        EnsureInitialized();

                        await operation(
                                cancellationToken)
                            .ConfigureAwait(true);
                    },
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (_lifetimeCancellation
                .IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
            when (Volatile.Read(
                ref _disposeState) != 0)
        {
        }
        catch (Exception exception)
        {
            ReportOperationFailure(
                exception);
        }
    }

    private void ReportOperationFailure(
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(
            exception);

        LastOperationException =
            exception;

        OperationFailed?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void UnsubscribeFromStartupEvents()
    {
        _startupCoordinator.ActivationRequested -=
            HandleActivationRequested;

        _startupCoordinator.HotkeyActivated -=
            HandleHotkeyActivated;

        _startupCoordinator.OpenRequested -=
            HandleOpenRequested;

        _startupCoordinator.SettingsRequested -=
            HandleSettingsRequested;

        _startupCoordinator.ExitRequested -=
            HandleExitRequested;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException(
                "Window management has not been initialized.");
        }
    }

    private static RectInt32 CreateRectangle(
        WindowPlacementResult placement)
    {
        ArgumentNullException.ThrowIfNull(
            placement);

        return new RectInt32(
            ToInteger(
                placement.Left,
                nameof(placement.Left)),
            ToInteger(
                placement.Top,
                nameof(placement.Top)),
            ToPositiveInteger(
                placement.Width,
                nameof(placement.Width)),
            ToPositiveInteger(
                placement.Height,
                nameof(placement.Height)));
    }

    private static int ToInteger(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value) ||
            value < int.MinValue ||
            value > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The window coordinate is outside the supported range.");
        }

        return checked((int)Math.Round(
            value,
            MidpointRounding.AwayFromZero));
    }

    private static int ToPositiveInteger(
        double value,
        string parameterName)
    {
        int result =
            ToInteger(
                value,
                parameterName);

        if (result <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The window size must be positive.");
        }

        return result;
    }

    private static double GetRasterizationScale(
        FrameworkElement root)
    {
        double scale =
            root.XamlRoot?.RasterizationScale ??
            1;

        return double.IsFinite(scale) &&
               scale > 0
            ? scale
            : 1;
    }

    private static bool IsAtCalculatedPosition(
        PointInt32 position,
        WindowPlacementResult? placement)
    {
        if (placement is null)
        {
            return false;
        }

        return position.X ==
                   ToInteger(
                       placement.Left,
                       nameof(placement.Left)) &&
               position.Y ==
                   ToInteger(
                       placement.Top,
                       nameof(placement.Top));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(
                ref _disposeState) != 0,
            this);
    }
}
