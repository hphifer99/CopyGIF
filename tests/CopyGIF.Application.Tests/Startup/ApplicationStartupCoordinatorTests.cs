using CopyGIF.Application.Onboarding;
using CopyGIF.Application.Settings;
using CopyGIF.Application.Startup;
using CopyGIF.Application.Updates;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Startup;

[TestClass]
public sealed class ApplicationStartupCoordinatorTests
{
    [TestMethod]
    public async Task InitializeAsync_SecondaryInstance_RedirectsAndStops()
    {
        Harness harness =
            new();

        harness.SingleInstanceService.Result =
            new SingleInstanceResult
            {
                Status =
                    SingleInstanceStatus
                        .RedirectedToPrimary
            };

        string[] arguments =
        [
            "--open"
        ];

        ApplicationStartupResult result =
            await harness.Coordinator.InitializeAsync(
                arguments);

        Assert.AreEqual(
            ApplicationStartupStatus.RedirectedToPrimary,
            result.Status);

        Assert.IsTrue(
            result.ShouldExit);

        CollectionAssert.AreEqual(
            arguments,
            harness.SingleInstanceService
                .InitializationArguments
                .Single()
                .ToArray());

        Assert.AreEqual(
            0,
            harness.Paths.EnsureDirectoriesCallCount);

        Assert.AreEqual(
            0,
            harness.MigrationCoordinator.CallCount);

        Assert.AreEqual(
            0,
            harness.SettingsCoordinator.LoadCallCount);

        Assert.AreEqual(
            0,
            harness.TrayService.InitializeCallCount);
    }

    [TestMethod]
    public async Task InitializeAsync_MigrationFails_ReturnsSafeFailure()
    {
        Harness harness =
            new();

        harness.MigrationCoordinator.Result =
            new MigrationResult
            {
                Status =
                    MigrationStatus.RolledBack,

                Message =
                    "The prior data was restored."
            };

        ApplicationStartupResult result =
            await harness.Coordinator.InitializeAsync(
                []);

        Assert.AreEqual(
            ApplicationStartupStatus.MigrationFailed,
            result.Status);

        Assert.AreEqual(
            "The prior data was restored.",
            result.Message);

        Assert.AreEqual(
            1,
            harness.Paths.EnsureDirectoriesCallCount);

        Assert.AreEqual(
            1,
            harness.MigrationCoordinator.CallCount);

        Assert.AreEqual(
            0,
            harness.SettingsCoordinator.LoadCallCount);

        Assert.AreEqual(
            0,
            harness.StartupService.IsEnabledCallCount);

        Assert.AreEqual(
            0,
            harness.TrayService.InitializeCallCount);
    }

    [TestMethod]
    public async Task InitializeAsync_PrimaryInstance_InitializesRuntimeServices()
    {
        AppSettings settings =
            CreateSettings(
                hotkey:
                    "Ctrl+Alt+G",
                startWithWindows:
                    true);

        OnboardingState onboarding =
            CreateOnboardingState(
                isRequired:
                    false);

        Harness harness =
            new(
                settings,
                onboarding);

        harness.StartupService.IsEnabled =
            false;

        ApplicationStartupResult result =
            await harness.Coordinator.InitializeAsync(
                []);

        Assert.IsTrue(
            result.IsReady);

        Assert.AreSame(
            settings,
            result.Settings);

        Assert.AreSame(
            onboarding,
            result.Onboarding);

        Assert.AreEqual(
            "Ctrl+Alt+G",
            harness.HotkeyService.RegisteredGesture);

        Assert.IsEmpty(harness.StartupService.RequestedStates);

        Assert.AreEqual(
            1,
            harness.TrayService.InitializeCallCount);
    }

    [TestMethod]
    public async Task InitializeAsync_OnboardingRequired_PreservesOnboardingState()
    {
        OnboardingState onboarding =
            CreateOnboardingState(
                isRequired:
                    true);

        Harness harness =
            new(
                onboarding:
                    onboarding);

        harness.StartupService.IsEnabled =
            true;

        ApplicationStartupResult result =
            await harness.Coordinator.InitializeAsync(
                []);

        Assert.IsTrue(
            result.IsReady);

        Assert.IsNotNull(
            result.Onboarding);

        Assert.IsTrue(
            result.Onboarding.IsRequired);
    }

    [TestMethod]
    public async Task InitializeAsync_HotkeyConflict_KeepsTrayAvailableForRecovery()
    {
        AppSettings settings =
            CreateSettings(
                hotkey:
                    "Ctrl+Alt+G");

        Harness harness =
            new(
                settings);

        harness.HotkeyService.RegistrationHandler =
            static (_, _) =>
                Task.FromResult(
                    HotkeyRegistrationResult.Failed(
                        HotkeyRegistrationFailure.Conflict,
                        "That hotkey is already in use."));

        ApplicationStartupResult result =
            await harness.Coordinator.InitializeAsync(
                []);

        Assert.AreEqual(
            ApplicationStartupStatus.Ready,
            result.Status);

        Assert.AreEqual(
            HotkeyRegistrationFailure.Conflict,
            result.HotkeyFailure);

        Assert.AreEqual(
            "That hotkey is already in use.",
            result.Message);

        Assert.IsNull(
            harness.HotkeyService.RegisteredGesture);

        Assert.IsEmpty(harness.StartupService.RequestedStates);

        Assert.AreEqual(
            1,
            harness.TrayService.InitializeCallCount);
    }

    [TestMethod]
    public async Task InitializeAsync_RuntimeAlreadyMatches_DoesNotRewriteRegistrations()
    {
        AppSettings settings =
            CreateSettings(
                hotkey:
                    "Ctrl+Alt+G",
                startWithWindows:
                    true);

        Harness harness =
            new(
                settings);

        await harness.HotkeyService
            .TryRegisterAsync(
                settings.Hotkey);

        harness.StartupService.IsEnabled =
            true;

        ApplicationStartupResult result =
            await harness.Coordinator.InitializeAsync(
                []);

        Assert.IsTrue(
            result.IsReady);

        Assert.HasCount(
            1,
            harness.HotkeyService.RegistrationAttempts);

        Assert.HasCount(
            0,
            harness.StartupService.RequestedStates);
    }

    [TestMethod]
    public async Task InitializeAsync_WindowsStartupWasDisabled_DoesNotPreventLaunch()
    {
        AppSettings settings =
            CreateSettings(
                hotkey:
                    "Ctrl+Alt+G",
                startWithWindows:
                    true);

        Harness harness =
            new(
                settings);

        harness.StartupService.IsEnabled =
            false;

        harness.StartupService.SetEnabledHandler =
            static (enabled, _) =>
                enabled
                    ? throw new IOException(
                        "Startup registration failed.")
                    : Task.CompletedTask;

        ApplicationStartupResult result = await harness.Coordinator.InitializeAsync([]);
        Assert.IsTrue(result.IsReady);
        Assert.AreEqual("Ctrl+Alt+G", harness.HotkeyService.RegisteredGesture);
        Assert.AreEqual(0, harness.HotkeyService.UnregisterCallCount);
        Assert.IsEmpty(harness.StartupService.RequestedStates);
        Assert.AreEqual(0, harness.StartupService.IsEnabledCallCount);
        Assert.AreEqual(1, harness.TrayService.InitializeCallCount);
    }

    [TestMethod]
    public async Task InitializeAsync_TrayInitializationFails_RollsBackRuntimeState()
    {
        AppSettings settings =
            CreateSettings(
                hotkey:
                    "Ctrl+Alt+H",
                startWithWindows:
                    false);

        Harness harness =
            new(
                settings);

        await harness.HotkeyService
            .TryRegisterAsync(
                "Ctrl+Alt+G");

        harness.StartupService.IsEnabled =
            true;

        harness.TrayService.InitializeHandler =
            static _ =>
                throw new IOException(
                    "Tray initialization failed.");

        await Assert.ThrowsExactlyAsync<IOException>(
            () =>
                harness.Coordinator.InitializeAsync(
                    []));

        Assert.AreEqual(
            "Ctrl+Alt+G",
            harness.HotkeyService.RegisteredGesture);

        Assert.IsTrue(
            harness.StartupService.IsEnabled);

        Assert.IsEmpty(harness.StartupService.RequestedStates);
    }

    [TestMethod]
    public async Task InitializeAsync_CancelledDuringTrayInitialization_RollsBackWithFreshToken()
    {
        AppSettings settings =
            CreateSettings(
                hotkey:
                    "Ctrl+Alt+H",
                startWithWindows:
                    false);

        Harness harness =
            new(
                settings);

        await harness.HotkeyService
            .TryRegisterAsync(
                "Ctrl+Alt+G");

        harness.StartupService.IsEnabled =
            true;

        using CancellationTokenSource cancellation =
            new();

        harness.TrayService.InitializeHandler =
            token =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();

                return Task.CompletedTask;
            };

        await Assert.ThrowsExactlyAsync<
            OperationCanceledException>(
            () =>
                harness.Coordinator.InitializeAsync(
                    [],
                    cancellation.Token));

        Assert.AreEqual(
            "Ctrl+Alt+G",
            harness.HotkeyService.RegisteredGesture);

        Assert.IsTrue(
            harness.StartupService.IsEnabled);
    }

    [TestMethod]
    public async Task InitializeAsync_RepeatedCall_ReturnsCachedResult()
    {
        Harness harness =
            new();

        harness.StartupService.IsEnabled =
            true;

        ApplicationStartupResult first =
            await harness.Coordinator.InitializeAsync(
                []);

        ApplicationStartupResult second =
            await harness.Coordinator.InitializeAsync(
                [
                    "--ignored-after-startup"
                ]);

        Assert.AreSame(
            first,
            second);

        Assert.HasCount(
            1,
            harness.SingleInstanceService
                .InitializationArguments);

        Assert.AreEqual(
            1,
            harness.MigrationCoordinator.CallCount);

        Assert.AreEqual(
            1,
            harness.TrayService.InitializeCallCount);
    }

    [TestMethod]
    public void RuntimeEvents_AreForwardedByCoordinator()
    {
        Harness harness =
            new();

        int activationCount =
            0;

        int hotkeyCount =
            0;

        int openCount =
            0;

        int settingsCount =
            0;

        int exitCount =
            0;

        IReadOnlyList<string>? activationArguments =
            null;

        harness.Coordinator.ActivationRequested +=
            (_, eventArgs) =>
            {
                activationCount++;

                activationArguments =
                    eventArgs.Arguments;
            };

        harness.Coordinator.HotkeyActivated +=
            (_, _) =>
                hotkeyCount++;

        harness.Coordinator.OpenRequested +=
            (_, _) =>
                openCount++;

        harness.Coordinator.SettingsRequested +=
            (_, _) =>
                settingsCount++;

        harness.Coordinator.ExitRequested +=
            (_, _) =>
                exitCount++;

        harness.SingleInstanceService
            .RaiseActivationRequested(
                "--open");

        harness.HotkeyService
            .RaiseActivated();

        harness.TrayService
            .RaiseOpenRequested();

        harness.TrayService
            .RaiseSettingsRequested();

        harness.TrayService
            .RaiseExitRequested();

        Assert.AreEqual(
            1,
            activationCount);

        CollectionAssert.AreEqual(
            new[]
            {
                "--open"
            },
            activationArguments!.ToArray());

        Assert.AreEqual(
            1,
            hotkeyCount);

        Assert.AreEqual(
            1,
            openCount);

        Assert.AreEqual(
            1,
            settingsCount);

        Assert.AreEqual(
            1,
            exitCount);
    }

    [TestMethod]
    public async Task Dispose_UnsubscribesEventsAndRejectsInitialization()
    {
        Harness harness =
            new();

        int eventCount =
            0;

        harness.Coordinator.OpenRequested +=
            (_, _) =>
                eventCount++;

        harness.Coordinator.Dispose();

        harness.TrayService
            .RaiseOpenRequested();

        Assert.AreEqual(
            0,
            eventCount);

        await Assert.ThrowsExactlyAsync<
            ObjectDisposedException>(
            () =>
                harness.Coordinator.InitializeAsync(
                    []));
    }

    [TestMethod]
    public async Task InitializeAsync_UnsupportedSingleInstanceStatus_Throws()
    {
        Harness harness =
            new();

        harness.SingleInstanceService.Result =
            new SingleInstanceResult
            {
                Status =
                    (SingleInstanceStatus)999
            };

        await Assert.ThrowsExactlyAsync<
            InvalidDataException>(
            () =>
                harness.Coordinator.InitializeAsync(
                    []));

        Assert.AreEqual(
            0,
            harness.Paths.EnsureDirectoriesCallCount);
    }

    [TestMethod]
    public async Task InitializeAsync_NullArguments_ThrowsBeforeSideEffects()
    {
        Harness harness =
            new();

        await Assert.ThrowsExactlyAsync<
            ArgumentNullException>(
            () =>
                harness.Coordinator.InitializeAsync(
                    null!));

        Assert.HasCount(
            0,
            harness.SingleInstanceService
                .InitializationArguments);
    }

    // ---- A deferred update is installed before anything else starts ----

    [TestMethod]
    public async Task InitializeAsync_DeferredUpdateStarted_StopsBeforeAnyWindowTrayOrHotkey()
    {
        FakePendingUpdateCoordinator updates =
            new(
                PendingUpdateInstallStatus.Started);

        Harness harness =
            new(
                updateCoordinator: updates,
                applicationVersion: "2.0.0.0");

        ApplicationStartupResult result =
            await harness.Coordinator.InitializeAsync(
                []);

        Assert.AreEqual(
            ApplicationStartupStatus.UpdateInstallStarted,
            result.Status);

        Assert.IsTrue(
            result.ShouldExit);

        Assert.IsFalse(
            result.IsReady);

        Assert.AreEqual(
            "2.0.0.0",
            updates.LastVersion);

        Assert.AreEqual(
            0,
            harness.SettingsCoordinator.LoadCallCount);

        Assert.AreEqual(
            0,
            harness.TrayService.InitializeCallCount);

        Assert.HasCount(
            0,
            harness.HotkeyService.RegistrationAttempts);
    }

    [TestMethod]
    public async Task InitializeAsync_DeferredUpdateStarted_StillRunsTheMigrationFirst()
    {
        FakePendingUpdateCoordinator updates =
            new(
                PendingUpdateInstallStatus.Started);

        Harness harness =
            new(
                updateCoordinator: updates,
                applicationVersion: "2.0.0.0");

        await harness.Coordinator.InitializeAsync(
            []);

        Assert.AreEqual(
            1,
            harness.MigrationCoordinator.CallCount);
    }

    [TestMethod]
    public async Task InitializeAsync_SecondaryInstance_NeverLooksAtDeferredUpdates()
    {
        FakePendingUpdateCoordinator updates =
            new(
                PendingUpdateInstallStatus.Started);

        Harness harness =
            new(
                updateCoordinator: updates,
                applicationVersion: "2.0.0.0");

        harness.SingleInstanceService.Result =
            new SingleInstanceResult
            {
                Status =
                    SingleInstanceStatus
                        .RedirectedToPrimary
            };

        ApplicationStartupResult result =
            await harness.Coordinator.InitializeAsync(
                []);

        Assert.AreEqual(
            ApplicationStartupStatus.RedirectedToPrimary,
            result.Status);

        Assert.AreEqual(
            0,
            updates.CallCount,
            "only the primary instance may install an update");
    }

    [TestMethod]
    [DataRow(PendingUpdateInstallStatus.NothingPending)]
    [DataRow(PendingUpdateInstallStatus.AlreadyCurrent)]
    [DataRow(PendingUpdateInstallStatus.NotApplicable)]
    [DataRow(PendingUpdateInstallStatus.PackageUnavailable)]
    [DataRow(PendingUpdateInstallStatus.VerificationFailed)]
    public async Task InitializeAsync_DeferredUpdateNotStarted_StartsNormally(
        PendingUpdateInstallStatus status)
    {
        FakePendingUpdateCoordinator updates =
            new(
                status);

        Harness harness =
            new(
                updateCoordinator: updates,
                applicationVersion: "2.0.0.0");

        ApplicationStartupResult result =
            await harness.Coordinator.InitializeAsync(
                []);

        Assert.AreEqual(
            ApplicationStartupStatus.Ready,
            result.Status);

        Assert.AreEqual(
            1,
            updates.CallCount);
    }

    [TestMethod]
    public async Task InitializeAsync_DeferredUpdateThrows_StartsNormally()
    {
        // A problem with an update must never keep CopyGIF from starting.
        FakePendingUpdateCoordinator updates =
            new(
                PendingUpdateInstallStatus.Started)
            {
                Failure =
                    new InvalidOperationException(
                        "Windows Installer could not be started.")
            };

        Harness harness =
            new(
                updateCoordinator: updates,
                applicationVersion: "2.0.0.0");

        ApplicationStartupResult result =
            await harness.Coordinator.InitializeAsync(
                []);

        Assert.AreEqual(
            ApplicationStartupStatus.Ready,
            result.Status);

        Assert.AreEqual(
            1,
            harness.TrayService.InitializeCallCount);
    }

    [TestMethod]
    public async Task InitializeAsync_NoUpdateCoordinatorOrVersion_StartsNormally()
    {
        Harness harness =
            new();

        ApplicationStartupResult result =
            await harness.Coordinator.InitializeAsync(
                []);

        Assert.AreEqual(
            ApplicationStartupStatus.Ready,
            result.Status);
    }

    private sealed class FakeApplicationVersion(
        string version) :
        IApplicationVersion
    {
        public string Current => version;
    }

    private sealed class FakePendingUpdateCoordinator(
        PendingUpdateInstallStatus status) :
        IUpdateCoordinator
    {
        public int CallCount { get; private set; }

        public string? LastVersion { get; private set; }

        public Exception? Failure { get; init; }

        public Task<PendingUpdateInstallResult> InstallPendingAsync(
            string currentVersion,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastVersion = currentVersion;

            if (Failure is not null)
            {
                throw Failure;
            }

            return Task.FromResult(
                new PendingUpdateInstallResult
                {
                    Status = status
                });
        }

        public Task<UpdateCheckResult> CheckAsync(
            string currentVersion,
            bool force = false,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UpdatePreparationResult> PrepareAsync(
            UpdateCandidate candidate,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UpdateInstallationResult> InstallAsync(
            DownloadedUpdatePackage package,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UpdateInstallationResult> InstallAsync(
            DownloadedUpdatePackage package,
            UpdateInstallOptions options,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SkipVersionAsync(
            string version,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeferInstallToNextLaunchAsync(
            DownloadedUpdatePackage package,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AutomaticUpdateResult> RunAutomaticAsync(
            string currentVersion,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private static AppSettings CreateSettings(
        string hotkey =
            AppSettings.DefaultHotkey,
        bool startWithWindows =
            true)
    {
        return new AppSettings
        {
            Hotkey =
                hotkey,

            Startup =
                new StartupSettings
                {
                    StartWithWindows =
                        startWithWindows
                }
        };
    }

    private static OnboardingState
        CreateOnboardingState(
            bool isRequired)
    {
        return new OnboardingState
        {
            IsRequired =
                isRequired,

            ProviderId =
                "klipy",

            ProviderDisplayName =
                "KLIPY",

            CredentialHelpUri =
                new Uri(
                    "https://partner.klipy.com/api-keys")
        };
    }

    private sealed class Harness
    {
        public Harness(
            AppSettings? settings = null,
            OnboardingState? onboarding = null,
            IUpdateCoordinator? updateCoordinator = null,
            string? applicationVersion = null)
        {
            SettingsCoordinator.Settings =
                settings ??
                CreateSettings();

            OnboardingCoordinator.State =
                onboarding ??
                CreateOnboardingState(
                    isRequired:
                        false);

            Coordinator =
                new ApplicationStartupCoordinator(
                    SingleInstanceService,
                    Paths,
                    MigrationCoordinator,
                    SettingsCoordinator,
                    OnboardingCoordinator,
                    HotkeyService,
                    StartupService,
                    TrayService,
                    updateCoordinator,
                    applicationVersion is null
                        ? null
                        : new FakeApplicationVersion(
                            applicationVersion));
        }

        public FakeSingleInstanceService
            SingleInstanceService
        { get; } =
            new();

        public FakeApplicationPaths Paths
        { get; } =
            new();

        public FakeMigrationCoordinator
            MigrationCoordinator
        { get; } =
            new();

        public FakeSettingsCoordinator
            SettingsCoordinator
        { get; } =
            new();

        public FakeOnboardingCoordinator
            OnboardingCoordinator
        { get; } =
            new();

        public FakeHotkeyService HotkeyService
        { get; } =
            new();

        public FakeStartupService StartupService
        { get; } =
            new();

        public FakeTrayService TrayService
        { get; } =
            new();

        public ApplicationStartupCoordinator Coordinator
        { get; }
    }

    private sealed class FakeSettingsCoordinator :
        ISettingsCoordinator
    {
        public AppSettings Settings
        {
            get;
            set;
        } =
            new();

        public int LoadCallCount
        {
            get;
            private set;
        }

        public Task<AppSettings> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            LoadCallCount++;

            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                Settings);
        }

        public Task<SettingsSaveResult> SaveAsync(
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            Settings =
                settings;

            return Task.FromResult(
                SettingsSaveResult.Success(
                    settings));
        }

        public Task<SettingsSaveResult>
            RestoreDefaultsAsync(
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            Settings =
                new AppSettings();

            return Task.FromResult(
                SettingsSaveResult.Success(
                    Settings));
        }

        public Task<SettingsSaveResult?>
            ChooseLibraryStorageRootAsync(
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult<
                SettingsSaveResult?>(
                null);
        }
    }

    private sealed class FakeOnboardingCoordinator :
        IOnboardingCoordinator
    {
        public IReadOnlyList<OnboardingProviderOption> Providers { get; } = [];

        public OnboardingState State
        {
            get;
            set;
        } =
            CreateOnboardingState(
                isRequired:
                    false);

        public int GetStateCallCount
        {
            get;
            private set;
        }

        public Uri CredentialHelpUri =>
            State.CredentialHelpUri;

        public Task<OnboardingState> GetStateAsync(
            CancellationToken cancellationToken = default)
        {
            GetStateCallCount++;

            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                State);
        }

        public Task<CredentialValidationResult>
            CompleteAsync(
                string credential,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                CredentialValidationResult.Valid());
        }

        public Task<bool> OpenCredentialHelpAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                true);
        }
    }
}
