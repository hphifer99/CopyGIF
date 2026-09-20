using CopyGIF.Application.Updates;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Updates;

// "Not now" installs the update automatically the next time CopyGIF starts, and an unreachable
// certificate revocation server means "try again later", never "the update is broken".
[TestClass]
public sealed class UpdateDeferralTests
{
    private static readonly DateTimeOffset ReferenceTime =
        new(
            2026,
            9,
            3,
            12,
            0,
            0,
            TimeSpan.Zero);

    // ---- Deferring ("Not now") ----

    [TestMethod]
    public async Task DeferInstallToNextLaunchAsync_PerUser_RemembersTheManifestAndTime()
    {
        Rig rig = new();

        UpdateManifest manifest =
            CreateManifest();

        bool scheduled =
            await rig.Coordinator.DeferInstallToNextLaunchAsync(
                CreatePackage(
                    manifest));

        Assert.IsTrue(scheduled);

        Assert.IsNotNull(
            rig.State.Value.PendingInstall);

        Assert.AreEqual(
            manifest,
            rig.State.Value.PendingInstall!.Manifest);

        Assert.AreEqual(
            ReferenceTime,
            rig.State.Value.PendingInstall.DeferredAtUtc);
    }

    [TestMethod]
    public async Task DeferInstallToNextLaunchAsync_PerMachine_RemembersNothing()
    {
        // A per-machine install needs the administrator (UAC) prompt, which must never appear
        // by itself while CopyGIF is starting.
        Rig rig = new(perUser: false);

        bool scheduled =
            await rig.Coordinator.DeferInstallToNextLaunchAsync(
                CreatePackage(
                    CreateManifest()));

        Assert.IsFalse(scheduled);

        Assert.IsNull(
            rig.State.Value.PendingInstall);

        Assert.HasCount(
            0,
            rig.State.SavedStates);
    }

    [TestMethod]
    public async Task DeferInstallToNextLaunchAsync_StoreInstall_RemembersNothing()
    {
        Rig rig = new();

        rig.Channel.Context =
            new InstallationContext
            {
                Channel = InstallChannel.MicrosoftStore,
                Scope = InstallScope.CurrentUser
            };

        Assert.IsFalse(
            await rig.Coordinator.DeferInstallToNextLaunchAsync(
                CreatePackage(
                    CreateManifest())));

        Assert.IsNull(
            rig.State.Value.PendingInstall);
    }

    [TestMethod]
    public async Task DeferInstallToNextLaunchAsync_NewerUpdate_ReplacesTheOlderOne()
    {
        Rig rig = new();

        await rig.Coordinator.DeferInstallToNextLaunchAsync(
            CreatePackage(
                CreateManifest("2.1.0")));

        rig.Clock.UtcNow =
            ReferenceTime.AddDays(3);

        await rig.Coordinator.DeferInstallToNextLaunchAsync(
            CreatePackage(
                CreateManifest("2.2.0")));

        Assert.AreEqual(
            "2.2.0",
            rig.State.Value.PendingInstall!.Manifest.Version);
    }

    [TestMethod]
    public async Task DeferInstallToNextLaunchAsync_KeepsTheRestOfTheUpdateState()
    {
        Rig rig = new();

        rig.State.Value =
            new UpdateState
            {
                LastCheckedAtUtc = ReferenceTime,
                LastAvailableVersion = "2.1.0",
                SkippedVersion = "2.0.5"
            };

        await rig.Coordinator.DeferInstallToNextLaunchAsync(
            CreatePackage(
                CreateManifest()));

        Assert.AreEqual(
            ReferenceTime,
            rig.State.Value.LastCheckedAtUtc);

        Assert.AreEqual(
            "2.1.0",
            rig.State.Value.LastAvailableVersion);

        Assert.AreEqual(
            "2.0.5",
            rig.State.Value.SkippedVersion);
    }

    [TestMethod]
    public async Task SkipVersionAsync_SameVersionAsTheRememberedNotNow_CancelsIt()
    {
        Rig rig = new();

        rig.State.Value =
            new UpdateState
            {
                PendingInstall = CreatePending("2.1.0")
            };

        await rig.Coordinator.SkipVersionAsync(
            "2.1.0");

        Assert.IsNull(
            rig.State.Value.PendingInstall);

        Assert.AreEqual(
            "2.1.0",
            rig.State.Value.SkippedVersion);
    }

    [TestMethod]
    public async Task SkipVersionAsync_DifferentVersion_KeepsTheRememberedNotNow()
    {
        Rig rig = new();

        rig.State.Value =
            new UpdateState
            {
                PendingInstall = CreatePending("2.2.0")
            };

        await rig.Coordinator.SkipVersionAsync(
            "2.1.0");

        Assert.IsNotNull(
            rig.State.Value.PendingInstall);
    }

    [TestMethod]
    public async Task InstallAsync_Started_ClearsTheRememberedNotNow()
    {
        Rig rig = new();

        rig.State.Value =
            new UpdateState
            {
                PendingInstall = CreatePending("2.1.0")
            };

        UpdateInstallationResult result =
            await rig.Coordinator.InstallAsync(
                CreatePackage(
                    CreateManifest()),
                new UpdateInstallOptions
                {
                    RestartApplication = true
                });

        Assert.AreEqual(
            UpdateInstallationStatus.Installed,
            result.Status);

        Assert.IsNull(
            rig.State.Value.PendingInstall);
    }

    // ---- Installing at start ----

    [TestMethod]
    public async Task InstallPendingAsync_NothingRemembered_DoesNothing()
    {
        Rig rig = new();

        PendingUpdateInstallResult result =
            await rig.Coordinator.InstallPendingAsync(
                "2.0.0");

        Assert.AreEqual(
            PendingUpdateInstallStatus.NothingPending,
            result.Status);

        Assert.IsFalse(
            result.ShouldExit);

        Assert.HasCount(
            0,
            rig.Installer.InstallationRequests);

        Assert.HasCount(
            0,
            rig.State.SavedStates);

        Assert.HasCount(
            0,
            rig.Packages.FindExistingRequests);
    }

    [TestMethod]
    public async Task InstallPendingAsync_PerUser_StartsASilentRestartingInstallWithoutUacOrNetwork()
    {
        Rig rig = new();

        UpdateManifest manifest =
            CreateManifest();

        rig.State.Value =
            new UpdateState
            {
                PendingInstall =
                    new PendingUpdateInstall
                    {
                        Manifest = manifest,
                        DeferredAtUtc = ReferenceTime
                    }
            };

        PendingUpdateInstallResult result =
            await rig.Coordinator.InstallPendingAsync(
                "2.0.0");

        Assert.AreEqual(
            PendingUpdateInstallStatus.Started,
            result.Status);

        Assert.IsTrue(
            result.ShouldExit);

        Assert.AreEqual(
            "2.1.0",
            result.Version);

        Assert.HasCount(
            1,
            rig.Installer.InstallationRequests);

        UpdateInstallOptions options =
            rig.Installer.InstallationOptions[0];

        Assert.IsTrue(
            options.Silent,
            "nobody is looking at CopyGIF at start, so no installer window");

        Assert.IsTrue(
            options.RestartApplication,
            "the installer must start the updated CopyGIF");

        Assert.IsFalse(
            options.RequiresElevation,
            "a per-user install never raises the administrator prompt");

        Assert.IsFalse(
            options.CheckRevocationOnline,
            "starting CopyGIF must not wait for the network");

        Assert.IsTrue(
            rig.Installer.VerificationOptions.All(
                verification =>
                    !verification.CheckRevocationOnline),
            "every check at start leaves out the online revocation lookup");

        Assert.IsNull(
            rig.State.Value.PendingInstall,
            "the record is used up");
    }

    [TestMethod]
    public async Task InstallPendingAsync_ChecksThePackageAgainBeforeInstalling()
    {
        Rig rig = new();

        rig.State.Value =
            new UpdateState
            {
                PendingInstall = CreatePending("2.1.0")
            };

        await rig.Coordinator.InstallPendingAsync(
            "2.0.0");

        Assert.HasCount(
            1,
            rig.Packages.FindExistingRequests);

        Assert.IsGreaterThanOrEqualTo(
            1,
            rig.Installer.VerificationRequests.Count);
    }

    [TestMethod]
    public async Task InstallPendingAsync_ClearsTheRecordBeforeTryingToInstall()
    {
        // A record that survived a failing attempt would retry at every start, forever.
        Rig rig = new();

        rig.State.Value =
            new UpdateState
            {
                PendingInstall = CreatePending("2.1.0")
            };

        bool recordWasGoneDuringTheAttempt = false;

        rig.Installer.InstallationHandler =
            (_, _) =>
            {
                recordWasGoneDuringTheAttempt =
                    rig.State.Value.PendingInstall is null;

                return Task.CompletedTask;
            };

        await rig.Coordinator.InstallPendingAsync(
            "2.0.0");

        Assert.IsTrue(
            recordWasGoneDuringTheAttempt);
    }

    [TestMethod]
    public async Task InstallPendingAsync_InstallerCannotStart_StillClearsTheRecord()
    {
        Rig rig = new();

        rig.State.Value =
            new UpdateState
            {
                PendingInstall = CreatePending("2.1.0")
            };

        rig.Installer.InstallationHandler =
            static (_, _) =>
                throw new InvalidOperationException(
                    "Windows Installer could not be started.");

        await Assert.ThrowsExactlyAsync<
            InvalidOperationException>(
            () => rig.Coordinator.InstallPendingAsync(
                "2.0.0"));

        Assert.IsNull(
            rig.State.Value.PendingInstall,
            "one automatic attempt per remembered update, even a failed one");

        // A second start finds nothing to retry.
        PendingUpdateInstallResult second =
            await rig.Coordinator.InstallPendingAsync(
                "2.0.0");

        Assert.AreEqual(
            PendingUpdateInstallStatus.NothingPending,
            second.Status);
    }

    [TestMethod]
    [DataRow("2.0.0")]
    [DataRow("2.1.0")]
    [DataRow("3.0.0")]
    public async Task InstallPendingAsync_AlreadyAtOrPastThatVersion_InstallsNothingAndClearsTheRecord(
        string runningVersion)
    {
        Rig rig = new();

        rig.State.Value =
            new UpdateState
            {
                PendingInstall = CreatePending("2.0.0")
            };

        PendingUpdateInstallResult result =
            await rig.Coordinator.InstallPendingAsync(
                runningVersion);

        Assert.AreEqual(
            PendingUpdateInstallStatus.AlreadyCurrent,
            result.Status);

        Assert.HasCount(
            0,
            rig.Installer.InstallationRequests);

        Assert.IsNull(
            rig.State.Value.PendingInstall);
    }

    [TestMethod]
    public async Task InstallPendingAsync_UnparseableVersion_InstallsNothingAndClearsTheRecord()
    {
        Rig rig = new();

        rig.State.Value =
            new UpdateState
            {
                PendingInstall =
                    new PendingUpdateInstall
                    {
                        Manifest =
                            CreateManifest() with
                            {
                                Version = "not a version"
                            },
                        DeferredAtUtc = ReferenceTime
                    }
            };

        PendingUpdateInstallResult result =
            await rig.Coordinator.InstallPendingAsync(
                "2.0.0");

        Assert.AreEqual(
            PendingUpdateInstallStatus.AlreadyCurrent,
            result.Status);

        Assert.HasCount(
            0,
            rig.Installer.InstallationRequests);

        Assert.IsNull(
            rig.State.Value.PendingInstall);
    }

    [TestMethod]
    public async Task InstallPendingAsync_PerMachine_InstallsNothing()
    {
        Rig rig = new(perUser: false);

        rig.State.Value =
            new UpdateState
            {
                PendingInstall = CreatePending("2.1.0")
            };

        PendingUpdateInstallResult result =
            await rig.Coordinator.InstallPendingAsync(
                "2.0.0");

        Assert.AreEqual(
            PendingUpdateInstallStatus.NotApplicable,
            result.Status);

        Assert.HasCount(
            0,
            rig.Installer.InstallationRequests);

        Assert.IsNull(
            rig.State.Value.PendingInstall);
    }

    [TestMethod]
    public async Task InstallPendingAsync_UpdatesTurnedOffSinceNotNow_InstallsNothing()
    {
        Rig rig = new();

        rig.Settings.Value =
            CreateSettings(
                checkForUpdates: false);

        rig.State.Value =
            new UpdateState
            {
                PendingInstall = CreatePending("2.1.0")
            };

        PendingUpdateInstallResult result =
            await rig.Coordinator.InstallPendingAsync(
                "2.0.0");

        Assert.AreEqual(
            PendingUpdateInstallStatus.NotApplicable,
            result.Status);

        Assert.HasCount(
            0,
            rig.Installer.InstallationRequests);
    }

    [TestMethod]
    public async Task InstallPendingAsync_ChangedToNotifyOnlySinceNotNow_InstallsNothing()
    {
        Rig rig = new();

        rig.Settings.Value =
            CreateSettings(
                mode: UpdateMode.NotifyOnly);

        rig.State.Value =
            new UpdateState
            {
                PendingInstall = CreatePending("2.1.0")
            };

        PendingUpdateInstallResult result =
            await rig.Coordinator.InstallPendingAsync(
                "2.0.0");

        Assert.AreEqual(
            PendingUpdateInstallStatus.NotApplicable,
            result.Status);

        Assert.HasCount(
            0,
            rig.Installer.InstallationRequests);
    }

    [TestMethod]
    public async Task InstallPendingAsync_VersionSkippedSinceNotNow_InstallsNothing()
    {
        Rig rig = new();

        rig.State.Value =
            new UpdateState
            {
                SkippedVersion = "2.1.0",
                PendingInstall = CreatePending("2.1.0")
            };

        PendingUpdateInstallResult result =
            await rig.Coordinator.InstallPendingAsync(
                "2.0.0");

        Assert.AreEqual(
            PendingUpdateInstallStatus.NotApplicable,
            result.Status);

        Assert.HasCount(
            0,
            rig.Installer.InstallationRequests);
    }

    [TestMethod]
    public async Task InstallPendingAsync_PackageFileGone_InstallsNothingAndClearsTheRecord()
    {
        Rig rig = new();

        rig.Packages.FindExistingHandler =
            static (_, _) =>
                Task.FromResult<DownloadedUpdatePackage?>(
                    null);

        rig.State.Value =
            new UpdateState
            {
                PendingInstall = CreatePending("2.1.0")
            };

        PendingUpdateInstallResult result =
            await rig.Coordinator.InstallPendingAsync(
                "2.0.0");

        Assert.AreEqual(
            PendingUpdateInstallStatus.PackageUnavailable,
            result.Status);

        Assert.HasCount(
            0,
            rig.Installer.InstallationRequests);

        Assert.IsNull(
            rig.State.Value.PendingInstall);
    }

    [TestMethod]
    public async Task InstallPendingAsync_PackageFailsVerification_DeletesItAndInstallsNothing()
    {
        Rig rig = new();

        rig.Installer.VerificationHandler =
            static (_, _) =>
                Task.FromResult(
                    UpdatePackageVerificationResult.Invalid(
                        UpdatePackageVerificationFailure.HashMismatch,
                        "The package changed."));

        rig.State.Value =
            new UpdateState
            {
                PendingInstall = CreatePending("2.1.0")
            };

        PendingUpdateInstallResult result =
            await rig.Coordinator.InstallPendingAsync(
                "2.0.0");

        Assert.AreEqual(
            PendingUpdateInstallStatus.VerificationFailed,
            result.Status);

        Assert.IsFalse(
            result.ShouldExit);

        Assert.HasCount(
            0,
            rig.Installer.InstallationRequests);

        Assert.HasCount(
            1,
            rig.Packages.DeletedPackages);

        Assert.IsNull(
            rig.State.Value.PendingInstall);
    }

    // ---- An unreachable revocation server means "later" ----

    [TestMethod]
    public async Task PrepareAsync_RevocationServersUnreachable_KeepsThePackageAndDefers()
    {
        Rig rig = new();

        rig.Installer.VerificationHandler =
            static (_, _) =>
                Task.FromResult(
                    CreateRevocationUnavailable());

        UpdatePreparationResult result =
            await rig.Coordinator.PrepareAsync(
                CreateCandidate());

        Assert.AreEqual(
            UpdatePreparationStatus.VerificationDeferred,
            result.Status);

        Assert.IsFalse(
            result.IsReady);

        Assert.IsNull(
            result.Package,
            "a package that was not confirmed must not be offered for installation");

        Assert.HasCount(
            0,
            rig.Packages.DeletedPackages,
            "the package is not damaged, so it stays for the next attempt");
    }

    [TestMethod]
    public async Task PrepareAsync_GenuineSignatureFailure_StillDeletesThePackage()
    {
        Rig rig = new();

        rig.Installer.VerificationHandler =
            static (_, _) =>
                Task.FromResult(
                    UpdatePackageVerificationResult.Invalid(
                        UpdatePackageVerificationFailure.InvalidSignature,
                        "bad signature"));

        UpdatePreparationResult result =
            await rig.Coordinator.PrepareAsync(
                CreateCandidate());

        Assert.AreEqual(
            UpdatePreparationStatus.VerificationFailed,
            result.Status);

        Assert.HasCount(
            1,
            rig.Packages.DeletedPackages);
    }

    [TestMethod]
    public async Task RunAutomaticAsync_RevocationServersUnreachable_RetriesLaterQuietly()
    {
        Rig rig = new();

        rig.Feed.LatestManifest =
            CreateManifest();

        rig.Installer.VerificationHandler =
            static (_, _) =>
                Task.FromResult(
                    CreateRevocationUnavailable());

        AutomaticUpdateResult result =
            await rig.Coordinator.RunAutomaticAsync(
                "2.0.0");

        Assert.AreEqual(
            AutomaticUpdateAction.RetryLater,
            result.Action);

        Assert.HasCount(
            0,
            rig.Packages.DeletedPackages);

        Assert.IsNull(
            rig.State.Value.LastCheckedAtUtc,
            "the check time is cleared, or the next attempt would be a day or a week away");
    }

    [TestMethod]
    public async Task InstallAsync_RevocationServersUnreachable_DoesNotInstallAndKeepsThePackage()
    {
        Rig rig = new();

        rig.Installer.VerificationHandler =
            static (_, _) =>
                Task.FromResult(
                    CreateRevocationUnavailable());

        UpdateInstallationResult result =
            await rig.Coordinator.InstallAsync(
                CreatePackage(
                    CreateManifest()));

        Assert.AreEqual(
            UpdateInstallationStatus.VerificationDeferred,
            result.Status);

        Assert.IsFalse(
            result.WasInstalled);

        Assert.HasCount(
            0,
            rig.Installer.InstallationRequests);

        Assert.HasCount(
            0,
            rig.Packages.DeletedPackages);
    }

    [TestMethod]
    public async Task InstallAsync_DefaultOptions_ChecksRevocationOnline()
    {
        Rig rig = new();

        await rig.Coordinator.InstallAsync(
            CreatePackage(
                CreateManifest()));

        Assert.IsTrue(
            rig.Installer.VerificationOptions.All(
                verification =>
                    verification.CheckRevocationOnline));
    }

    // ---- Helpers ----

    private static UpdatePackageVerificationResult
        CreateRevocationUnavailable() =>
        UpdatePackageVerificationResult.Invalid(
            UpdatePackageVerificationFailure
                .RevocationCheckUnavailable,
            "The certificate servers could not be reached.");

    private static AppSettings CreateSettings(
        bool checkForUpdates = true,
        UpdateMode mode = UpdateMode.Recommended) =>
        new()
        {
            Updates =
                new UpdateSettings
                {
                    CheckForUpdates = checkForUpdates,
                    CheckFrequency = UpdateCheckFrequency.Daily,
                    Mode = mode
                }
        };

    private static UpdateManifest CreateManifest(
        string version = "2.1.0") =>
        new()
        {
            Version = version,
            Channel = "stable",
            AssetName =
                $"CopyGIF-{version}-x64.msi",
            AssetUri =
                new Uri(
                    $"https://github.com/hphifer99/CopyGIF/releases/download/v{version}/CopyGIF-{version}-x64.msi"),
            SizeBytes = 1024,
            Sha256 =
                new string(
                    'a',
                    64),
            MinimumSupportedVersion = "2.0.0",
            ReleaseNotesUri =
                new Uri(
                    $"https://github.com/hphifer99/CopyGIF/releases/tag/v{version}"),
            PublishedAtUtc = ReferenceTime
        };

    private static PendingUpdateInstall CreatePending(
        string version) =>
        new()
        {
            Manifest = CreateManifest(version),
            DeferredAtUtc = ReferenceTime
        };

    private static UpdateCandidate CreateCandidate() =>
        new()
        {
            CurrentVersion = "2.0.0",
            Manifest = CreateManifest()
        };

    private static DownloadedUpdatePackage CreatePackage(
        UpdateManifest manifest) =>
        new()
        {
            Manifest = manifest,
            FilePath =
                Path.Combine(
                    Path.GetTempPath(),
                    manifest.AssetName),
            SizeBytes = manifest.SizeBytes,
            Sha256 = manifest.Sha256,
            DownloadedAtUtc = ReferenceTime
        };

    private sealed class Rig
    {
        public Rig(
            bool perUser = true)
        {
            Settings.Value =
                CreateSettings();

            Clock.UtcNow =
                ReferenceTime;

            Channel.Context =
                new InstallationContext
                {
                    Channel = InstallChannel.Msi,
                    Scope =
                        perUser
                            ? InstallScope.CurrentUser
                            : InstallScope.AllUsers
                };

            Coordinator =
                new UpdateCoordinator(
                    Settings,
                    State,
                    Feed,
                    Packages,
                    Installer,
                    Channel,
                    Clock);
        }

        public FakeSettingsStore Settings { get; } =
            new();

        public FakeUpdateStateStore State { get; } =
            new();

        public FakeUpdateFeed Feed { get; } =
            new();

        public FakeUpdatePackageService Packages { get; } =
            new();

        public FakeUpdateInstaller Installer { get; } =
            new();

        public FakeInstallChannelService Channel { get; } =
            new();

        public FakeClock Clock { get; } =
            new();

        public UpdateCoordinator Coordinator { get; }
    }
}
