using System.Runtime.ExceptionServices;
using CopyGIF.Application.Onboarding;
using CopyGIF.Application.Settings;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Startup;

public sealed class ApplicationStartupCoordinator :
    IApplicationStartupCoordinator,
    IDisposable
{
    private readonly ISingleInstanceService
        _singleInstanceService;

    private readonly IApplicationPaths _paths;

    private readonly IMigrationCoordinator
        _migrationCoordinator;

    private readonly ISettingsCoordinator
        _settingsCoordinator;

    private readonly IOnboardingCoordinator
        _onboardingCoordinator;

    private readonly IHotkeyService _hotkeyService;

    private readonly IStartupService _startupService;

    private readonly ITrayService _trayService;

    private readonly SemaphoreSlim _gate =
        new(
            initialCount: 1,
            maxCount: 1);

    private ApplicationStartupResult? _result;

    private bool _disposed;

    public ApplicationStartupCoordinator(
        ISingleInstanceService singleInstanceService,
        IApplicationPaths paths,
        IMigrationCoordinator migrationCoordinator,
        ISettingsCoordinator settingsCoordinator,
        IOnboardingCoordinator onboardingCoordinator,
        IHotkeyService hotkeyService,
        IStartupService startupService,
        ITrayService trayService)
    {
        _singleInstanceService =
            singleInstanceService ??
            throw new ArgumentNullException(
                nameof(singleInstanceService));

        _paths =
            paths ??
            throw new ArgumentNullException(
                nameof(paths));

        _migrationCoordinator =
            migrationCoordinator ??
            throw new ArgumentNullException(
                nameof(migrationCoordinator));

        _settingsCoordinator =
            settingsCoordinator ??
            throw new ArgumentNullException(
                nameof(settingsCoordinator));

        _onboardingCoordinator =
            onboardingCoordinator ??
            throw new ArgumentNullException(
                nameof(onboardingCoordinator));

        _hotkeyService =
            hotkeyService ??
            throw new ArgumentNullException(
                nameof(hotkeyService));

        _startupService =
            startupService ??
            throw new ArgumentNullException(
                nameof(startupService));

        _trayService =
            trayService ??
            throw new ArgumentNullException(
                nameof(trayService));

        _singleInstanceService.ActivationRequested +=
            HandleActivationRequested;

        _hotkeyService.Activated +=
            HandleHotkeyActivated;

        _trayService.OpenRequested +=
            HandleOpenRequested;

        _trayService.SettingsRequested +=
            HandleSettingsRequested;

        _trayService.ExitRequested +=
            HandleExitRequested;
    }

    public event EventHandler<ActivationRequestedEventArgs>?
        ActivationRequested;

    public event EventHandler? HotkeyActivated;

    public event EventHandler? OpenRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public async Task<ApplicationStartupResult>
        InitializeAsync(
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            arguments);

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            if (_result is not null)
            {
                return _result;
            }

            _result =
                await InitializeCoreAsync(
                        arguments,
                        cancellationToken)
                    .ConfigureAwait(false);

            return _result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _singleInstanceService.ActivationRequested -=
            HandleActivationRequested;

        _hotkeyService.Activated -=
            HandleHotkeyActivated;

        _trayService.OpenRequested -=
            HandleOpenRequested;

        _trayService.SettingsRequested -=
            HandleSettingsRequested;

        _trayService.ExitRequested -=
            HandleExitRequested;

        _gate.Dispose();
    }

    private async Task<ApplicationStartupResult>
        InitializeCoreAsync(
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
    {
        SingleInstanceResult singleInstance =
            await _singleInstanceService
                .InitializeAsync(
                    arguments,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!Enum.IsDefined(
                singleInstance.Status))
        {
            throw new InvalidDataException(
                "The single-instance service returned an unsupported status.");
        }

        if (!singleInstance.IsPrimaryInstance)
        {
            return new ApplicationStartupResult
            {
                Status =
                    ApplicationStartupStatus
                        .RedirectedToPrimary,
                SingleInstance = singleInstance
            };
        }

        _paths.EnsureDirectoriesExist();

        MigrationResult migration =
            await _migrationCoordinator
                .MigrateIfNeededAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (!migration.Succeeded)
        {
            return new ApplicationStartupResult
            {
                Status =
                    ApplicationStartupStatus
                        .MigrationFailed,
                SingleInstance = singleInstance,
                Migration = migration,
                Message =
                    string.IsNullOrWhiteSpace(
                        migration.Message)
                        ? "CopyGIF could not safely migrate its saved data."
                        : migration.Message
            };
        }

        AppSettings settings =
            await _settingsCoordinator
                .LoadAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        OnboardingState onboarding =
            await _onboardingCoordinator
                .GetStateAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        string? previousGesture =
            _hotkeyService.RegisteredGesture;

        bool previousStartupState =
            await _startupService
                .IsEnabledAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        bool hotkeyChanged = false;
        bool startupChanged = false;

        try
        {
            if (!string.Equals(
                    previousGesture,
                    settings.Hotkey,
                    StringComparison.OrdinalIgnoreCase))
            {
                HotkeyRegistrationResult hotkeyResult =
                    await _hotkeyService
                        .TryRegisterAsync(
                            settings.Hotkey,
                            cancellationToken)
                        .ConfigureAwait(false);

                if (!hotkeyResult.Succeeded)
                {
                    return new ApplicationStartupResult
                    {
                        Status =
                            ApplicationStartupStatus
                                .HotkeyRejected,
                        SingleInstance = singleInstance,
                        Migration = migration,
                        Settings = settings,
                        Onboarding = onboarding,
                        HotkeyFailure =
                            hotkeyResult.Failure,
                        Message = hotkeyResult.Message
                    };
                }

                hotkeyChanged = true;
            }

            if (previousStartupState !=
                settings.Startup.StartWithWindows)
            {
                startupChanged = true;

                await _startupService
                    .SetEnabledAsync(
                        settings.Startup.StartWithWindows,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await _trayService
                .InitializeAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            return new ApplicationStartupResult
            {
                Status =
                    ApplicationStartupStatus.Ready,
                SingleInstance = singleInstance,
                Migration = migration,
                Settings = settings,
                Onboarding = onboarding
            };
        }
        catch (Exception exception)
        {
            IReadOnlyList<Exception> rollbackFailures =
                await RollbackRuntimeStateAsync(
                        startupChanged,
                        previousStartupState,
                        hotkeyChanged,
                        previousGesture)
                    .ConfigureAwait(false);

            if (rollbackFailures.Count > 0)
            {
                throw new AggregateException(
                    "Application startup failed and one or more runtime rollback operations also failed.",
                    [exception, .. rollbackFailures]);
            }

            ExceptionDispatchInfo
                .Capture(
                    exception)
                .Throw();

            throw;
        }
    }

    private async Task<IReadOnlyList<Exception>>
        RollbackRuntimeStateAsync(
            bool startupChanged,
            bool previousStartupState,
            bool hotkeyChanged,
            string? previousGesture)
    {
        List<Exception> failures = [];

        if (startupChanged)
        {
            try
            {
                await _startupService
                    .SetEnabledAsync(
                        previousStartupState,
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add(
                    exception);
            }
        }

        if (hotkeyChanged)
        {
            try
            {
                if (previousGesture is null)
                {
                    await _hotkeyService
                        .UnregisterAsync(
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                else
                {
                    HotkeyRegistrationResult rollbackResult =
                        await _hotkeyService
                            .TryRegisterAsync(
                                previousGesture,
                                CancellationToken.None)
                            .ConfigureAwait(false);

                    if (!rollbackResult.Succeeded)
                    {
                        failures.Add(
                            new InvalidOperationException(
                                rollbackResult.Message ??
                                "The previous hotkey could not be restored."));
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Add(
                    exception);
            }
        }

        return failures;
    }

    private void HandleActivationRequested(
        object? sender,
        ActivationRequestedEventArgs eventArgs)
    {
        ActivationRequested?.Invoke(
            this,
            eventArgs);
    }

    private void HandleHotkeyActivated(
        object? sender,
        EventArgs eventArgs)
    {
        HotkeyActivated?.Invoke(
            this,
            eventArgs);
    }

    private void HandleOpenRequested(
        object? sender,
        EventArgs eventArgs)
    {
        OpenRequested?.Invoke(
            this,
            eventArgs);
    }

    private void HandleSettingsRequested(
        object? sender,
        EventArgs eventArgs)
    {
        SettingsRequested?.Invoke(
            this,
            eventArgs);
    }

    private void HandleExitRequested(
        object? sender,
        EventArgs eventArgs)
    {
        ExitRequested?.Invoke(
            this,
            eventArgs);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}
