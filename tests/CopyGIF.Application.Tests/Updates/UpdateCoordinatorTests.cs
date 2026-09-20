using CopyGIF.Application.Updates;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Updates;

[TestClass]
public sealed class UpdateCoordinatorTests
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

    [TestMethod]
    public async Task CheckAsync_Disabled_DoesNotContactFeed()
    {
        Harness harness =
            new(
                CreateSettings(
                    checkForUpdates: false));

        UpdateCheckResult result =
            await harness.Coordinator.CheckAsync(
                "2.0.0");

        Assert.AreEqual(
            UpdateCheckStatus.Disabled,
            result.Status);

        Assert.HasCount(
            0,
            harness.UpdateFeed.RequestedChannels);

        Assert.HasCount(
            0,
            harness.StateStore.SavedStates);
    }

    [TestMethod]
    public async Task CheckAsync_StoreInstall_DoesNotContactOrInvokeMsiUpdater()
    {
        Harness harness =
            new();

        harness.InstallChannelService.Context =
            new InstallationContext
            {
                Channel = InstallChannel.MicrosoftStore,
                Scope = InstallScope.CurrentUser
            };

        UpdateCheckResult result =
            await harness.Coordinator.CheckAsync(
                "2.0.0");

        Assert.AreEqual(
            UpdateCheckStatus.ManagedByStore,
            result.Status);

        Assert.HasCount(
            0,
            harness.UpdateFeed.RequestedChannels);

        Assert.HasCount(
            0,
            harness.PackageService.DownloadRequests);

        Assert.HasCount(
            0,
            harness.Installer.InstallationRequests);
    }

    [TestMethod]
    public async Task CheckAsync_NotDue_DoesNotContactFeed()
    {
        Harness harness =
            new();

        harness.StateStore.Value =
            new UpdateState
            {
                LastCheckedAtUtc =
                    ReferenceTime.AddHours(-2)
            };

        UpdateCheckResult result =
            await harness.Coordinator.CheckAsync(
                "2.0.0");

        Assert.AreEqual(
            UpdateCheckStatus.NotDue,
            result.Status);

        Assert.HasCount(
            0,
            harness.UpdateFeed.RequestedChannels);
    }

    [TestMethod]
    public async Task CheckAsync_Forced_BypassesFrequencyGate()
    {
        Harness harness =
            new();

        harness.StateStore.Value =
            new UpdateState
            {
                LastCheckedAtUtc = ReferenceTime
            };

        UpdateCheckResult result =
            await harness.Coordinator.CheckAsync(
                "2.0.0",
                force: true);

        Assert.AreEqual(
            UpdateCheckStatus.FeedUnavailable,
            result.Status);

        CollectionAssert.AreEqual(
            new[]
            {
                "stable"
            },
            harness.UpdateFeed.RequestedChannels.ToArray());

        Assert.AreEqual(
            ReferenceTime,
            harness.StateStore.Value.LastCheckedAtUtc);
    }

    [TestMethod]
    public async Task CheckAsync_NewerVersion_ReturnsCandidateAndSavesState()
    {
        Harness harness =
            new();

        harness.UpdateFeed.LatestManifest =
            CreateManifest(
                version: "2.1.0",
                minimumSupportedVersion: "2.0.0");

        UpdateCheckResult result =
            await harness.Coordinator.CheckAsync(
                "2.0.0");

        Assert.IsTrue(
            result.HasUpdate);

        Assert.IsNotNull(
            result.Candidate);

        Assert.IsFalse(
            result.Candidate.IsRequired);

        Assert.AreEqual(
            "2.1.0",
            harness.StateStore.Value
                .LastAvailableVersion);

        Assert.AreEqual(
            ReferenceTime,
            harness.StateStore.Value
                .LastCheckedAtUtc);
    }

    [TestMethod]
    public async Task CheckAsync_UnsupportedInstalledVersion_MarksUpdateRequired()
    {
        Harness harness =
            new();

        harness.UpdateFeed.LatestManifest =
            CreateManifest(
                version: "2.1.0",
                minimumSupportedVersion: "2.0.0");

        UpdateCheckResult result =
            await harness.Coordinator.CheckAsync(
                "1.5.0");

        Assert.IsNotNull(
            result.Candidate);

        Assert.IsTrue(
            result.Candidate.IsRequired);
    }

    [TestMethod]
    public async Task CheckAsync_StableRelease_IsNewerThanPrerelease()
    {
        Harness harness =
            new();

        harness.UpdateFeed.LatestManifest =
            CreateManifest(
                version: "2.0.0",
                minimumSupportedVersion: "1.0.0");

        UpdateCheckResult result =
            await harness.Coordinator.CheckAsync(
                "2.0.0-rc.1");

        Assert.IsTrue(
            result.HasUpdate);
    }

    [TestMethod]
    public async Task CheckAsync_CurrentOrOlderRelease_ReturnsNoUpdate()
    {
        Harness harness =
            new();

        harness.UpdateFeed.LatestManifest =
            CreateManifest(
                version: "2.0.0",
                minimumSupportedVersion: "1.0.0");

        UpdateCheckResult result =
            await harness.Coordinator.CheckAsync(
                "2.1.0");

        Assert.AreEqual(
            UpdateCheckStatus.NoUpdateAvailable,
            result.Status);

        Assert.IsNull(
            result.Candidate);

        Assert.IsNull(
            harness.StateStore.Value
                .LastAvailableVersion);
    }

    [TestMethod]
    public async Task CheckAsync_InvalidManifest_ThrowsWithoutSavingCheck()
    {
        Harness harness =
            new();

        harness.UpdateFeed.LatestManifest =
            CreateManifest() with
            {
                Sha256 = "not-a-sha256"
            };

        await Assert.ThrowsExactlyAsync<
            InvalidDataException>(
            () => harness.Coordinator.CheckAsync(
                "2.0.0"));

        Assert.HasCount(
            0,
            harness.StateStore.SavedStates);
    }

    [TestMethod]
    public async Task PrepareAsync_ValidPackage_VerifiesAndRecordsDownload()
    {
        Harness harness =
            new();

        UpdateCandidate candidate =
            CreateCandidate();

        UpdatePreparationResult result =
            await harness.Coordinator.PrepareAsync(
                candidate);

        Assert.IsTrue(
            result.IsReady);

        Assert.IsNotNull(
            result.Package);

        Assert.HasCount(
            1,
            harness.PackageService.DownloadRequests);

        Assert.HasCount(
            1,
            harness.Installer.VerificationRequests);

        Assert.AreEqual(
            candidate.AvailableVersion,
            harness.StateStore.Value
                .LastDownloadedVersion);

        Assert.AreEqual(
            ReferenceTime,
            harness.StateStore.Value
                .LastDownloadedAtUtc);
    }

    [TestMethod]
    public async Task PrepareAsync_InvalidPackage_DeletesPackageAndDoesNotRecordDownload()
    {
        Harness harness =
            new();

        harness.Installer.VerificationHandler =
            static (_, _) =>
                Task.FromResult(
                    UpdatePackageVerificationResult.Invalid(
                        UpdatePackageVerificationFailure
                            .HashMismatch,
                        "The package hash did not match."));

        UpdatePreparationResult result =
            await harness.Coordinator.PrepareAsync(
                CreateCandidate());

        Assert.AreEqual(
            UpdatePreparationStatus.VerificationFailed,
            result.Status);

        Assert.IsNull(
            result.Package);

        Assert.HasCount(
            1,
            harness.PackageService.DeletedPackages);

        Assert.HasCount(
            0,
            harness.StateStore.SavedStates);
    }

    [TestMethod]
    public async Task PrepareAsync_MismatchedManifest_DeletesWithoutCallingInstaller()
    {
        Harness harness =
            new();

        UpdateManifest unexpectedManifest =
            CreateManifest(
                version: "9.0.0");

        harness.PackageService.DownloadHandler =
            (manifest, _, _) =>
                Task.FromResult(
                    CreatePackage(
                        unexpectedManifest));

        UpdatePreparationResult result =
            await harness.Coordinator.PrepareAsync(
                CreateCandidate());

        Assert.AreEqual(
            UpdatePreparationStatus.VerificationFailed,
            result.Status);

        Assert.HasCount(
            0,
            harness.Installer.VerificationRequests);

        Assert.HasCount(
            1,
            harness.PackageService.DeletedPackages);
    }

    [TestMethod]
    public async Task PrepareAsync_VerificationCancelled_DeletesDownloadedPackage()
    {
        Harness harness =
            new();

        using CancellationTokenSource cancellation =
            new();

        harness.Installer.VerificationHandler =
            (_, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();

                return Task.FromResult(
                    UpdatePackageVerificationResult.Valid());
            };

        await Assert.ThrowsExactlyAsync<
            OperationCanceledException>(
            () => harness.Coordinator.PrepareAsync(
                CreateCandidate(),
                cancellationToken:
                    cancellation.Token));

        Assert.HasCount(
            1,
            harness.PackageService.DeletedPackages);
    }

    [TestMethod]
    public async Task PrepareAsync_StateWriteFails_DeletesDownloadedPackage()
    {
        Harness harness =
            new();

        harness.StateStore.SaveHandler =
            static (_, _) =>
                throw new IOException(
                    "Update-state write failed.");

        await Assert.ThrowsExactlyAsync<IOException>(
            () => harness.Coordinator.PrepareAsync(
                CreateCandidate()));

        Assert.HasCount(
            1,
            harness.PackageService.DeletedPackages);

        Assert.IsNull(
            harness.StateStore.Value
                .LastDownloadedVersion);
    }

    [TestMethod]
    public async Task InstallAsync_StoreInstall_NeverInvokesMsiInstaller()
    {
        Harness harness =
            new();

        harness.InstallChannelService.Context =
            new InstallationContext
            {
                Channel = InstallChannel.MicrosoftStore,
                Scope = InstallScope.CurrentUser
            };

        UpdateInstallationResult result =
            await harness.Coordinator.InstallAsync(
                CreatePackage(
                    CreateManifest()));

        Assert.AreEqual(
            UpdateInstallationStatus.ManagedExternally,
            result.Status);

        Assert.HasCount(
            0,
            harness.Installer.VerificationRequests);

        Assert.HasCount(
            0,
            harness.Installer.InstallationRequests);
    }

    [TestMethod]
    public async Task InstallAsync_ValidMsi_ReverifiesBeforeInstallation()
    {
        Harness harness =
            new();

        DownloadedUpdatePackage package =
            CreatePackage(
                CreateManifest());

        UpdateInstallationResult result =
            await harness.Coordinator.InstallAsync(
                package);

        Assert.IsTrue(
            result.WasInstalled);

        Assert.AreSame(
            package,
            harness.Installer
                .VerificationRequests
                .Single());

        Assert.AreSame(
            package,
            harness.Installer
                .InstallationRequests
                .Single());
    }

    [TestMethod]
    public async Task InstallAsync_ReverificationFails_DeletesAndDoesNotInstall()
    {
        Harness harness =
            new();

        harness.Installer.VerificationHandler =
            static (_, _) =>
                Task.FromResult(
                    UpdatePackageVerificationResult.Invalid(
                        UpdatePackageVerificationFailure
                            .InvalidSignature,
                        "The signature is no longer valid."));

        UpdateInstallationResult result =
            await harness.Coordinator.InstallAsync(
                CreatePackage(
                    CreateManifest()));

        Assert.AreEqual(
            UpdateInstallationStatus.VerificationFailed,
            result.Status);

        Assert.HasCount(
            0,
            harness.Installer.InstallationRequests);

        Assert.HasCount(
            1,
            harness.PackageService.DeletedPackages);
    }

    [TestMethod]
    public async Task InstallAsync_VerificationCancelled_DeletesPackage()
    {
        Harness harness =
            new();

        using CancellationTokenSource cancellation =
            new();

        harness.Installer.VerificationHandler =
            (_, token) =>
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();

                return Task.FromResult(
                    UpdatePackageVerificationResult.Valid());
            };

        await Assert.ThrowsExactlyAsync<
            OperationCanceledException>(
            () => harness.Coordinator.InstallAsync(
                CreatePackage(
                    CreateManifest()),
                cancellation.Token));

        Assert.HasCount(
            1,
            harness.PackageService.DeletedPackages);

        Assert.HasCount(
            0,
            harness.Installer.InstallationRequests);
    }

    [TestMethod]
    public async Task RunAutomaticAsync_NotifyOnly_DoesNotDownload()
    {
        Harness harness =
            new(
                CreateSettings(
                    mode: UpdateMode.NotifyOnly));

        harness.UpdateFeed.LatestManifest =
            CreateManifest();

        AutomaticUpdateResult result =
            await harness.Coordinator.RunAutomaticAsync(
                "2.0.0");

        Assert.AreEqual(
            AutomaticUpdateAction.Notify,
            result.Action);

        Assert.HasCount(
            0,
            harness.PackageService.DownloadRequests);
    }

    [TestMethod]
    public async Task RunAutomaticAsync_RecommendedAllUsers_DownloadsAndPrompts()
    {
        Harness harness =
            new(
                CreateSettings(
                    mode: UpdateMode.Recommended));

        harness.UpdateFeed.LatestManifest =
            CreateManifest();

        AutomaticUpdateResult result =
            await harness.Coordinator.RunAutomaticAsync(
                "2.0.0");

        Assert.AreEqual(
            AutomaticUpdateAction.Prompt,
            result.Action);

        Assert.IsNotNull(
            result.Preparation);

        Assert.IsTrue(
            result.Preparation.IsReady);

        Assert.HasCount(
            0,
            harness.Installer.InstallationRequests);
    }

    [TestMethod]
    public async Task RunAutomaticAsync_DownloadAndInstall_AllUsersPromptsAfterDownload()
    {
        Harness harness =
            new(
                CreateSettings(
                    mode:
                        UpdateMode.DownloadAndInstall));

        harness.UpdateFeed.LatestManifest =
            CreateManifest();

        AutomaticUpdateResult result =
            await harness.Coordinator.RunAutomaticAsync(
                "2.0.0");

        Assert.AreEqual(
            AutomaticUpdateAction.Prompt,
            result.Action);

        Assert.IsNotNull(result.Preparation);
        Assert.IsTrue(result.Preparation.IsReady);

        Assert.HasCount(
            0,
            harness.Installer.InstallationRequests);
    }

    [TestMethod]
    public async Task RunAutomaticAsync_RecommendedPerUser_PreparesAndLeavesInstallToTheHost()
    {
        Harness harness =
            new(
                CreateSettings(
                    mode: UpdateMode.Recommended));

        harness.InstallChannelService.Context =
            new InstallationContext
            {
                Channel = InstallChannel.Msi,
                Scope = InstallScope.CurrentUser
            };

        harness.UpdateFeed.LatestManifest =
            CreateManifest();

        AutomaticUpdateResult result =
            await harness.Coordinator.RunAutomaticAsync(
                "2.0.0");

        Assert.AreEqual(
            AutomaticUpdateAction.Prompt,
            result.Action);

        Assert.AreEqual(
            UpdateMode.DownloadAndInstall,
            result.Check.ResolvedMode,
            "A per-user install resolves Recommended to automatic installation.");

        Assert.IsTrue(result.Preparation!.IsReady);

        Assert.HasCount(
            0,
            harness.Installer.InstallationRequests,
            "Installing means restarting CopyGIF, so the host decides when.");
    }

    [TestMethod]
    public async Task InstallAsync_PerUser_DoesNotRequestElevation()
    {
        Harness harness =
            new();

        harness.InstallChannelService.Context =
            new InstallationContext
            {
                Channel = InstallChannel.Msi,
                Scope = InstallScope.CurrentUser
            };

        await harness.Coordinator.InstallAsync(
            CreatePackage(
                CreateManifest()),
            new UpdateInstallOptions
            {
                Silent = true,
                RestartApplication = true
            });

        Assert.HasCount(
            1,
            harness.Installer.InstallationOptions);

        Assert.IsFalse(
            harness.Installer.InstallationOptions[0].RequiresElevation);

        Assert.IsTrue(
            harness.Installer.InstallationOptions[0].Silent);
    }

    [TestMethod]
    public async Task InstallAsync_PerMachine_RequestsElevation()
    {
        Harness harness =
            new();

        harness.InstallChannelService.Context =
            new InstallationContext
            {
                Channel = InstallChannel.Msi,
                Scope = InstallScope.AllUsers
            };

        await harness.Coordinator.InstallAsync(
            CreatePackage(
                CreateManifest()));

        Assert.HasCount(
            1,
            harness.Installer.InstallationOptions);

        Assert.IsTrue(
            harness.Installer.InstallationOptions[0].RequiresElevation);
    }

    [TestMethod]
    public async Task RunAutomaticAsync_VerificationFailure_DoesNotInstall()
    {
        Harness harness =
            new(
                CreateSettings(
                    mode:
                        UpdateMode.DownloadAndInstall));

        harness.UpdateFeed.LatestManifest =
            CreateManifest();

        harness.Installer.VerificationHandler =
            static (_, _) =>
                Task.FromResult(
                    UpdatePackageVerificationResult.Invalid(
                        UpdatePackageVerificationFailure
                            .UntrustedPublisher,
                        "The publisher is not trusted."));

        AutomaticUpdateResult result =
            await harness.Coordinator.RunAutomaticAsync(
                "2.0.0");

        Assert.AreEqual(
            AutomaticUpdateAction.VerificationFailed,
            result.Action);

        Assert.HasCount(
            0,
            harness.Installer.InstallationRequests);
    }

    [TestMethod]
    public async Task RunAutomaticAsync_DownloadFails_ClearsLastCheckSoNextPassRetries()
    {
        Harness harness =
            new(
                CreateSettings(
                    mode: UpdateMode.Recommended));

        harness.UpdateFeed.LatestManifest =
            CreateManifest();

        harness.PackageService.DownloadHandler =
            static (_, _, _) =>
                throw new HttpRequestException(
                    "The connection was interrupted.");

        await Assert.ThrowsExactlyAsync<
            HttpRequestException>(
            () => harness.Coordinator.RunAutomaticAsync(
                "2.0.0"));

        Assert.IsNull(
            harness.StateStore.Value.LastCheckedAtUtc);

        harness.PackageService.DownloadHandler =
            null;

        AutomaticUpdateResult retry =
            await harness.Coordinator.RunAutomaticAsync(
                "2.0.0");

        Assert.AreEqual(
            AutomaticUpdateAction.Prompt,
            retry.Action);
    }

    [TestMethod]
    public async Task RunAutomaticAsync_SkippedVersion_DoesNotDownloadOrPrompt()
    {
        Harness harness =
            new(
                CreateSettings(
                    mode: UpdateMode.Recommended));

        harness.UpdateFeed.LatestManifest =
            CreateManifest();

        harness.StateStore.Value =
            new UpdateState
            {
                SkippedVersion = "2.1.0"
            };

        AutomaticUpdateResult result =
            await harness.Coordinator.RunAutomaticAsync(
                "2.0.0");

        Assert.AreEqual(
            AutomaticUpdateAction.None,
            result.Action);

        Assert.HasCount(
            0,
            harness.PackageService.DownloadRequests);

        Assert.IsTrue(
            result.Check.HasUpdate);
    }

    [TestMethod]
    public async Task RunAutomaticAsync_NewerVersionThanSkipped_DownloadsAndPrompts()
    {
        Harness harness =
            new(
                CreateSettings(
                    mode: UpdateMode.Recommended));

        harness.UpdateFeed.LatestManifest =
            CreateManifest(
                version: "2.2.0");

        harness.StateStore.Value =
            new UpdateState
            {
                SkippedVersion = "2.1.0"
            };

        AutomaticUpdateResult result =
            await harness.Coordinator.RunAutomaticAsync(
                "2.0.0");

        Assert.AreEqual(
            AutomaticUpdateAction.Prompt,
            result.Action);

        Assert.HasCount(
            1,
            harness.PackageService.DownloadRequests);
    }

    [TestMethod]
    public async Task RunAutomaticAsync_SkippedButRequiredVersion_StillDownloads()
    {
        Harness harness =
            new(
                CreateSettings(
                    mode: UpdateMode.Recommended));

        harness.UpdateFeed.LatestManifest =
            CreateManifest(
                minimumSupportedVersion: "2.1.0");

        harness.StateStore.Value =
            new UpdateState
            {
                SkippedVersion = "2.1.0"
            };

        AutomaticUpdateResult result =
            await harness.Coordinator.RunAutomaticAsync(
                "2.0.0");

        Assert.AreEqual(
            AutomaticUpdateAction.Prompt,
            result.Action);

        Assert.HasCount(
            1,
            harness.PackageService.DownloadRequests);
    }

    [TestMethod]
    public async Task SkipVersionAsync_PersistsTheVersionWithoutLosingOtherState()
    {
        Harness harness =
            new();

        harness.StateStore.Value =
            new UpdateState
            {
                LastAvailableVersion = "2.1.0",
                LastCheckedAtUtc = ReferenceTime
            };

        await harness.Coordinator.SkipVersionAsync(
            " 2.1.0 ");

        Assert.AreEqual(
            "2.1.0",
            harness.StateStore.Value.SkippedVersion);

        Assert.AreEqual(
            "2.1.0",
            harness.StateStore.Value.LastAvailableVersion);

        Assert.AreEqual(
            ReferenceTime,
            harness.StateStore.Value.LastCheckedAtUtc);
    }

    [TestMethod]
    public async Task RunAutomaticAsync_PrunesOldPackagesForTheRunningVersion()
    {
        Harness harness =
            new();

        harness.UpdateFeed.LatestManifest =
            CreateManifest();

        await harness.Coordinator.RunAutomaticAsync(
            "2.0.0");

        Assert.HasCount(
            1,
            harness.PackageService.PruneRequests);

        Assert.AreEqual(
            "2.0.0",
            harness.PackageService.PruneRequests[0]);
    }

    [TestMethod]
    public async Task RunAutomaticAsync_PruneFailure_DoesNotStopTheCheck()
    {
        Harness harness =
            new(
                CreateSettings(
                    mode: UpdateMode.Recommended));

        harness.UpdateFeed.LatestManifest =
            CreateManifest();

        harness.PackageService.PruneHandler =
            static (_, _) =>
                throw new IOException(
                    "The updates folder is locked.");

        AutomaticUpdateResult result =
            await harness.Coordinator.RunAutomaticAsync(
                "2.0.0");

        Assert.AreEqual(
            AutomaticUpdateAction.Prompt,
            result.Action);
    }

    [TestMethod]
    public async Task InstallAsync_WithOptions_PassesTheOptionsToTheInstaller()
    {
        Harness harness =
            new();

        UpdateManifest manifest =
            CreateManifest();

        UpdateInstallOptions options =
            new()
            {
                Silent = true,
                RestartApplication = true
            };

        UpdateInstallationResult result =
            await harness.Coordinator.InstallAsync(
                CreatePackage(
                    manifest),
                options);

        Assert.AreEqual(
            UpdateInstallationStatus.Installed,
            result.Status);

        Assert.HasCount(
            1,
            harness.Installer.InstallationOptions);

        // The coordinator adds the elevation flag from the real install scope (the default
        // harness is a per-machine MSI, which needs it), so compare by value.
        Assert.AreEqual(
            options,
            harness.Installer.InstallationOptions[0]);
    }

    [TestMethod]
    public async Task InstallAsync_WithoutOptions_UsesTheInteractiveDefault()
    {
        Harness harness =
            new();

        await harness.Coordinator.InstallAsync(
            CreatePackage(
                CreateManifest()));

        Assert.AreEqual(
            UpdateInstallOptions.Interactive,
            harness.Installer.InstallationOptions[0]);
    }

    [TestMethod]
    public async Task Dispose_RejectsFurtherOperations()
    {
        Harness harness =
            new();

        harness.Coordinator.Dispose();

        await Assert.ThrowsExactlyAsync<
            ObjectDisposedException>(
            () => harness.Coordinator.CheckAsync(
                "2.0.0"));
    }

    private static AppSettings CreateSettings(
        bool checkForUpdates = true,
        UpdateCheckFrequency frequency =
            UpdateCheckFrequency.Daily,
        UpdateMode mode = UpdateMode.Recommended)
    {
        return new AppSettings
        {
            Updates =
                new UpdateSettings
                {
                    CheckForUpdates =
                        checkForUpdates,
                    CheckFrequency = frequency,
                    Mode = mode
                }
        };
    }

    private static UpdateManifest CreateManifest(
        string version = "2.1.0",
        string minimumSupportedVersion = "2.0.0")
    {
        return new UpdateManifest
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
            MinimumSupportedVersion =
                minimumSupportedVersion,
            ReleaseNotesUri =
                new Uri(
                    $"https://github.com/hphifer99/CopyGIF/releases/tag/v{version}"),
            PublishedAtUtc = ReferenceTime
        };
    }

    private static UpdateCandidate CreateCandidate()
    {
        return new UpdateCandidate
        {
            CurrentVersion = "2.0.0",
            Manifest = CreateManifest()
        };
    }

    private static DownloadedUpdatePackage CreatePackage(
        UpdateManifest manifest)
    {
        return new DownloadedUpdatePackage
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
    }

    private sealed class Harness
    {
        public Harness(
            AppSettings? settings = null)
        {
            SettingsStore.Value =
                settings ?? CreateSettings();

            Clock.UtcNow = ReferenceTime;

            Coordinator =
                new UpdateCoordinator(
                    SettingsStore,
                    StateStore,
                    UpdateFeed,
                    PackageService,
                    Installer,
                    InstallChannelService,
                    Clock);
        }

        public FakeSettingsStore SettingsStore { get; } =
            new();

        public FakeUpdateStateStore StateStore { get; } =
            new();

        public FakeUpdateFeed UpdateFeed { get; } =
            new();

        public FakeUpdatePackageService PackageService { get; } =
            new();

        public FakeUpdateInstaller Installer { get; } =
            new();

        public FakeInstallChannelService
            InstallChannelService
        { get; } =
                new();

        public FakeClock Clock { get; } =
            new();

        public UpdateCoordinator Coordinator { get; }
    }
}
