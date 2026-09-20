using CopyGIF.Application.Updates;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Updates;

namespace CopyGIF.Presentation.Tests.Updates;

[TestClass]
public sealed class UpdateViewModelTests
{
    private static readonly DateTimeOffset
        ReferenceTime =
            new(
                2026,
                9,
                3,
                12,
                0,
                0,
                TimeSpan.Zero);

    [TestMethod]
    public void Commands_AreDisabledUntilVersionIsInitialized()
    {
        UpdateViewModel viewModel =
            new(
                new FakeUpdateCoordinator());

        Assert.IsFalse(
            viewModel.HasCurrentVersion);

        Assert.IsFalse(
            viewModel.CheckCommand
                .CanExecute(null));

        Assert.IsFalse(
            viewModel.RunAutomaticCommand
                .CanExecute(null));

        Assert.IsFalse(
            viewModel.PrepareCommand
                .CanExecute(null));

        Assert.IsFalse(
            viewModel.InstallCommand
                .CanExecute(null));
    }

    [TestMethod]
    public void Initialize_SetsVersionAndEnablesChecking()
    {
        UpdateViewModel viewModel =
            new(
                new FakeUpdateCoordinator());

        viewModel.Initialize(
            " 2.0.0 ");

        Assert.AreEqual(
            "2.0.0",
            viewModel.CurrentVersion);

        Assert.IsTrue(
            viewModel.HasCurrentVersion);

        Assert.IsTrue(
            viewModel.CheckCommand
                .CanExecute(null));

        Assert.IsTrue(
            viewModel.RunAutomaticCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task CheckCommand_ForcesManualCheck()
    {
        FakeUpdateCoordinator coordinator =
            new();

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        await viewModel
            .CheckCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            coordinator.CheckCount);

        Assert.AreEqual(
            "2.0.0",
            coordinator.LastCurrentVersion);

        Assert.IsTrue(
            coordinator.LastForce);

        Assert.AreEqual(
            UpdateCheckStatus.NoUpdateAvailable,
            viewModel.CheckStatus);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);

        Assert.AreEqual(
            UserMessageSeverity.Success,
            viewModel.Message?.Severity);
    }

    [TestMethod]
    public async Task CheckCommand_UpdateAvailable_ExposesCandidate()
    {
        UpdateCandidate candidate =
            CreateCandidate(
                isRequired: false);

        FakeUpdateCoordinator coordinator =
            new()
            {
                CheckResult =
                    CreateCheckResult(
                        UpdateCheckStatus.UpdateAvailable,
                        candidate)
            };

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        await viewModel
            .CheckCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsUpdateAvailable);

        Assert.IsFalse(
            viewModel.IsRequiredUpdate);

        Assert.AreEqual(
            "2.1.0",
            viewModel.AvailableVersion);

        Assert.AreEqual(
            candidate.ReleaseNotesUri,
            viewModel.ReleaseNotesUri);

        Assert.IsTrue(
            viewModel.PrepareCommand
                .CanExecute(null));

        Assert.AreEqual(
            "update_available",
            viewModel.Message?.Code);
    }

    [TestMethod]
    public async Task CheckCommand_RequiredUpdate_UsesWarningState()
    {
        FakeUpdateCoordinator coordinator =
            new()
            {
                CheckResult =
                    CreateCheckResult(
                        UpdateCheckStatus.UpdateAvailable,
                        CreateCandidate(
                            isRequired: true))
            };

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        await viewModel
            .CheckCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsRequiredUpdate);

        Assert.AreEqual(
            UserMessageSeverity.Warning,
            viewModel.Message?.Severity);

        Assert.AreEqual(
            "update_required",
            viewModel.Message?.Code);
    }

    [TestMethod]
    public async Task CheckCommand_StoreManaged_DisablesMsiActions()
    {
        FakeUpdateCoordinator coordinator =
            new()
            {
                CheckResult =
                    CreateCheckResult(
                        UpdateCheckStatus.ManagedByStore,
                        candidate: null,
                        channel:
                            InstallChannel.MicrosoftStore,
                        scope:
                            InstallScope.CurrentUser)
            };

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        await viewModel
            .CheckCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsManagedByStore);

        Assert.AreEqual(
            InstallChannel.MicrosoftStore,
            viewModel.InstallChannel);

        Assert.IsFalse(
            viewModel.PrepareCommand
                .CanExecute(null));

        Assert.IsFalse(
            viewModel.InstallCommand
                .CanExecute(null));

        Assert.AreEqual(
            UserMessageSeverity.Information,
            viewModel.Message?.Severity);
    }

    [TestMethod]
    public async Task PrepareCommand_DownloadsAndVerifiesPackage()
    {
        UpdateCandidate candidate =
            CreateCandidate();

        DownloadedUpdatePackage package =
            CreatePackage(
                candidate.Manifest);

        FakeUpdateCoordinator coordinator =
            new()
            {
                CheckResult =
                    CreateCheckResult(
                        UpdateCheckStatus.UpdateAvailable,
                        candidate),

                PreparationResult =
                    new UpdatePreparationResult
                    {
                        Status =
                            UpdatePreparationStatus.Ready,

                        Verification =
                            UpdatePackageVerificationResult.Valid(),

                        Package =
                            package
                    }
            };

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        await viewModel
            .CheckCommand
            .ExecuteAsync(null);

        await viewModel
            .PrepareCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            coordinator.PrepareCount);

        Assert.AreSame(
            candidate,
            coordinator.LastPreparedCandidate);

        Assert.IsTrue(
            coordinator.PrepareReceivedProgress);

        Assert.IsTrue(
            viewModel.IsPackageReady);

        Assert.AreSame(
            package,
            viewModel.PreparedPackage);

        Assert.AreEqual(
            100D,
            viewModel.DownloadPercentage);

        Assert.AreEqual(
            package.SizeBytes,
            viewModel.DownloadedBytes);

        Assert.AreEqual(
            package.SizeBytes,
            viewModel.TotalBytes);

        Assert.IsTrue(
            viewModel.InstallCommand
                .CanExecute(null));

        Assert.IsFalse(
            viewModel.PrepareCommand
                .CanExecute(null));

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);
    }

    [TestMethod]
    public async Task PrepareCommand_HashMismatch_DoesNotExposePackage()
    {
        UpdateCandidate candidate =
            CreateCandidate();

        FakeUpdateCoordinator coordinator =
            new()
            {
                CheckResult =
                    CreateCheckResult(
                        UpdateCheckStatus.UpdateAvailable,
                        candidate),

                PreparationResult =
                    new UpdatePreparationResult
                    {
                        Status =
                            UpdatePreparationStatus
                                .VerificationFailed,

                        Verification =
                            UpdatePackageVerificationResult.Invalid(
                                UpdatePackageVerificationFailure
                                    .HashMismatch,
                                "Hash mismatch.")
                    }
            };

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        await viewModel
            .CheckCommand
            .ExecuteAsync(null);

        await viewModel
            .PrepareCommand
            .ExecuteAsync(null);

        Assert.IsFalse(
            viewModel.IsPackageReady);

        Assert.IsNull(
            viewModel.PreparedPackage);

        Assert.AreEqual(
            AsyncOperationStatus.Failed,
            viewModel.OperationState.Status);

        Assert.AreEqual(
            "update_hash_mismatch",
            viewModel.Message?.Code);
    }

    [TestMethod]
    public async Task InstallCommand_InstallsPreparedPackage()
    {
        UpdateCandidate candidate =
            CreateCandidate();

        DownloadedUpdatePackage package =
            CreatePackage(
                candidate.Manifest);

        FakeUpdateCoordinator coordinator =
            new()
            {
                CheckResult =
                    CreateCheckResult(
                        UpdateCheckStatus.UpdateAvailable,
                        candidate),

                PreparationResult =
                    new UpdatePreparationResult
                    {
                        Status =
                            UpdatePreparationStatus.Ready,

                        Verification =
                            UpdatePackageVerificationResult.Valid(),

                        Package =
                            package
                    },

                InstallationResult =
                    new UpdateInstallationResult
                    {
                        Status =
                            UpdateInstallationStatus.Installed,

                        Verification =
                            UpdatePackageVerificationResult.Valid()
                    }
            };

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        await viewModel
            .CheckCommand
            .ExecuteAsync(null);

        await viewModel
            .PrepareCommand
            .ExecuteAsync(null);

        await viewModel
            .InstallCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            coordinator.InstallCount);

        Assert.AreSame(
            package,
            coordinator.LastInstalledPackage);

        Assert.IsNull(
            viewModel.PreparedPackage);

        Assert.IsFalse(
            viewModel.IsPackageReady);

        Assert.IsFalse(
            viewModel.InstallCommand
                .CanExecute(null));

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);
    }

    [TestMethod]
    public async Task InstallCommand_UsesRestartOptionsAndRequestsApplicationExit()
    {
        (UpdateViewModel viewModel, FakeUpdateCoordinator coordinator) =
            await CreateViewModelWithPreparedPackageAsync(
                UpdateInstallationStatus.Installed);

        int exitRequests = 0;

        viewModel.ApplicationExitRequested +=
            (_, _) => exitRequests++;

        await viewModel
            .InstallCommand
            .ExecuteAsync(null);

        Assert.AreSame(
            UpdateViewModel.RestartInstallOptions,
            coordinator.LastInstallOptions);

        Assert.IsTrue(
            coordinator.LastInstallOptions!.RestartApplication);

        Assert.IsFalse(
            coordinator.LastInstallOptions.Silent);

        Assert.AreEqual(
            1,
            exitRequests);
    }

    [TestMethod]
    public async Task InstallCommand_VerificationFailure_DoesNotRequestApplicationExit()
    {
        (UpdateViewModel viewModel, _) =
            await CreateViewModelWithPreparedPackageAsync(
                UpdateInstallationStatus.VerificationFailed);

        int exitRequests = 0;

        viewModel.ApplicationExitRequested +=
            (_, _) => exitRequests++;

        await viewModel
            .InstallCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            0,
            exitRequests);
    }

    [TestMethod]
    public async Task InstallInBackgroundAsync_UsesSilentRestartOptionsAndRequestsExit()
    {
        (UpdateViewModel viewModel, FakeUpdateCoordinator coordinator) =
            await CreateViewModelWithPreparedPackageAsync(
                UpdateInstallationStatus.Installed);

        int exitRequests = 0;

        viewModel.ApplicationExitRequested +=
            (_, _) => exitRequests++;

        await viewModel.InstallInBackgroundAsync();

        Assert.AreSame(
            UpdateViewModel.SilentRestartInstallOptions,
            coordinator.LastInstallOptions);

        Assert.IsTrue(
            coordinator.LastInstallOptions!.Silent);

        Assert.AreEqual(
            1,
            exitRequests);
    }

    [TestMethod]
    public async Task InstallInBackgroundAsync_WithoutPreparedPackage_DoesNothing()
    {
        FakeUpdateCoordinator coordinator =
            new();

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        int exitRequests = 0;

        viewModel.ApplicationExitRequested +=
            (_, _) => exitRequests++;

        await viewModel.InstallInBackgroundAsync();

        Assert.AreEqual(
            0,
            coordinator.InstallCount);

        Assert.AreEqual(
            0,
            exitRequests);
    }

    private static async Task<(UpdateViewModel ViewModel, FakeUpdateCoordinator Coordinator)>
        CreateViewModelWithPreparedPackageAsync(
            UpdateInstallationStatus installationStatus)
    {
        UpdateCandidate candidate =
            CreateCandidate();

        DownloadedUpdatePackage package =
            CreatePackage(
                candidate.Manifest);

        FakeUpdateCoordinator coordinator =
            new()
            {
                CheckResult =
                    CreateCheckResult(
                        UpdateCheckStatus.UpdateAvailable,
                        candidate),

                PreparationResult =
                    new UpdatePreparationResult
                    {
                        Status =
                            UpdatePreparationStatus.Ready,

                        Verification =
                            UpdatePackageVerificationResult.Valid(),

                        Package =
                            package
                    },

                InstallationResult =
                    new UpdateInstallationResult
                    {
                        Status =
                            installationStatus,

                        Verification =
                            installationStatus ==
                            UpdateInstallationStatus.Installed
                                ? UpdatePackageVerificationResult.Valid()
                                : UpdatePackageVerificationResult.Invalid(
                                    UpdatePackageVerificationFailure.Unknown,
                                    "Verification failed.")
                    }
            };

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        await viewModel
            .CheckCommand
            .ExecuteAsync(null);

        await viewModel
            .PrepareCommand
            .ExecuteAsync(null);

        return (viewModel, coordinator);
    }

    [TestMethod]
    public async Task InstallCommand_ReverificationFailure_ClearsPackage()
    {
        UpdateCandidate candidate =
            CreateCandidate();

        DownloadedUpdatePackage package =
            CreatePackage(
                candidate.Manifest);

        FakeUpdateCoordinator coordinator =
            new()
            {
                CheckResult =
                    CreateCheckResult(
                        UpdateCheckStatus.UpdateAvailable,
                        candidate),

                PreparationResult =
                    new UpdatePreparationResult
                    {
                        Status =
                            UpdatePreparationStatus.Ready,

                        Verification =
                            UpdatePackageVerificationResult.Valid(),

                        Package =
                            package
                    },

                InstallationResult =
                    new UpdateInstallationResult
                    {
                        Status =
                            UpdateInstallationStatus
                                .VerificationFailed,

                        Verification =
                            UpdatePackageVerificationResult.Invalid(
                                UpdatePackageVerificationFailure
                                    .InvalidSignature,
                                "Invalid signature.")
                    }
            };

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        await viewModel
            .CheckCommand
            .ExecuteAsync(null);

        await viewModel
            .PrepareCommand
            .ExecuteAsync(null);

        await viewModel
            .InstallCommand
            .ExecuteAsync(null);

        Assert.IsNull(
            viewModel.PreparedPackage);

        Assert.AreEqual(
            AsyncOperationStatus.Failed,
            viewModel.OperationState.Status);

        Assert.AreEqual(
            "update_signature_invalid",
            viewModel.Message?.Code);
    }

    [TestMethod]
    public async Task RunAutomaticCommand_Prompt_ExposesVerifiedPackage()
    {
        UpdateCandidate candidate =
            CreateCandidate();

        DownloadedUpdatePackage package =
            CreatePackage(
                candidate.Manifest);

        UpdateCheckResult check =
            CreateCheckResult(
                UpdateCheckStatus.UpdateAvailable,
                candidate);

        FakeUpdateCoordinator coordinator =
            new()
            {
                AutomaticResult =
                    new AutomaticUpdateResult
                    {
                        Action =
                            AutomaticUpdateAction.Prompt,

                        Check =
                            check,

                        Preparation =
                            new UpdatePreparationResult
                            {
                                Status =
                                    UpdatePreparationStatus.Ready,

                                Verification =
                                    UpdatePackageVerificationResult.Valid(),

                                Package =
                                    package
                            }
                    }
            };

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        await viewModel
            .RunAutomaticCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            coordinator.AutomaticCount);

        Assert.IsTrue(
            viewModel.IsUpdateAvailable);

        Assert.IsTrue(
            viewModel.IsPackageReady);

        Assert.AreSame(
            package,
            viewModel.PreparedPackage);

        Assert.IsTrue(
            viewModel.InstallCommand
                .CanExecute(null));

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);
    }

    [TestMethod]
    public async Task CancelCommand_CancelsRunningCheck()
    {
        FakeUpdateCoordinator coordinator =
            new()
            {
                BlockCheckUntilCancelled =
                    true
            };

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        Task execution =
            viewModel.CheckCommand
                .ExecuteAsync(null);

        await coordinator
            .CheckStarted
            .Task;

        Assert.IsTrue(
            viewModel.IsBusy);

        Assert.IsTrue(
            viewModel.CancelCommand
                .CanExecute(null));

        viewModel.CancelCommand
            .Execute(null);

        await execution;

        Assert.AreEqual(
            AsyncOperationStatus.Cancelled,
            viewModel.OperationState.Status);

        Assert.IsFalse(
            viewModel.IsBusy);

        Assert.IsFalse(
            viewModel.CancelCommand
                .CanExecute(null));
    }

    private static async Task<(UpdateViewModel ViewModel, FakeUpdateCoordinator Coordinator)>
        CreatePreparedViewModelAsync(
            InstallScope scope,
            UpdateInstallationResult installationResult)
    {
        UpdateCandidate candidate =
            CreateCandidate();

        FakeUpdateCoordinator coordinator =
            new()
            {
                CheckResult =
                    CreateCheckResult(
                        UpdateCheckStatus.UpdateAvailable,
                        candidate,
                        scope: scope),

                PreparationResult =
                    new UpdatePreparationResult
                    {
                        Status =
                            UpdatePreparationStatus.Ready,

                        Verification =
                            UpdatePackageVerificationResult.Valid(),

                        Package =
                            CreatePackage(
                                candidate.Manifest)
                    },

                InstallationResult =
                    installationResult
            };

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        await viewModel
            .CheckCommand
            .ExecuteAsync(null);

        await viewModel
            .PrepareCommand
            .ExecuteAsync(null);

        return (viewModel, coordinator);
    }

    private static readonly UpdateInstallationResult InstalledResult =
        new()
        {
            Status =
                UpdateInstallationStatus.Installed,

            Verification =
                UpdatePackageVerificationResult.Valid()
        };

    private static readonly UpdateInstallationResult DeferredInstallResult =
        new()
        {
            Status =
                UpdateInstallationStatus.VerificationDeferred,

            Verification =
                UpdatePackageVerificationResult.Invalid(
                    UpdatePackageVerificationFailure
                        .RevocationCheckUnavailable,
                    "The certificate servers could not be reached.")
        };

    [TestMethod]
    public async Task InstallCommand_PerUser_SaysCopyGifWillStartAgain()
    {
        (UpdateViewModel viewModel, _) =
            await CreatePreparedViewModelAsync(
                InstallScope.CurrentUser,
                InstalledResult);

        await viewModel
            .InstallCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.Message!.Text.Contains(
                "will close and start again"),
            viewModel.Message.Text);
    }

    [TestMethod]
    public async Task InstallCommand_PerMachine_DoesNotPromiseARestart()
    {
        // An elevated installer does not start CopyGIF (it would run with administrator rights).
        (UpdateViewModel viewModel, _) =
            await CreatePreparedViewModelAsync(
                InstallScope.AllUsers,
                InstalledResult);

        await viewModel
            .InstallCommand
            .ExecuteAsync(null);

        Assert.IsFalse(
            viewModel.Message!.Text.Contains(
                "start again"),
            viewModel.Message.Text);

        Assert.IsTrue(
            viewModel.Message.Text.Contains(
                "Open it again"),
            viewModel.Message.Text);
    }

    [TestMethod]
    public async Task InstallCommand_RevocationServersUnreachable_WarnsKeepsThePackageAndDoesNotExit()
    {
        (UpdateViewModel viewModel, _) =
            await CreatePreparedViewModelAsync(
                InstallScope.CurrentUser,
                DeferredInstallResult);

        int exitRequests = 0;

        viewModel.ApplicationExitRequested +=
            (_, _) => exitRequests++;

        await viewModel
            .InstallCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            0,
            exitRequests);

        Assert.IsNotNull(
            viewModel.PreparedPackage,
            "the package is fine, so the user can simply try again");

        Assert.IsTrue(
            viewModel.Message!.IsWarning);

        Assert.AreEqual(
            "update_revocation_unavailable",
            viewModel.Message.Code);
    }

    [TestMethod]
    public async Task PrepareCommand_RevocationServersUnreachable_IsAWarningNotAnError()
    {
        UpdateCandidate candidate =
            CreateCandidate();

        FakeUpdateCoordinator coordinator =
            new()
            {
                CheckResult =
                    CreateCheckResult(
                        UpdateCheckStatus.UpdateAvailable,
                        candidate),

                PreparationResult =
                    new UpdatePreparationResult
                    {
                        Status =
                            UpdatePreparationStatus
                                .VerificationDeferred,

                        Verification =
                            DeferredInstallResult.Verification
                    }
            };

        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                coordinator);

        await viewModel
            .CheckCommand
            .ExecuteAsync(null);

        await viewModel
            .PrepareCommand
            .ExecuteAsync(null);

        Assert.IsNull(
            viewModel.PreparedPackage);

        Assert.IsTrue(
            viewModel.Message!.IsWarning);

        Assert.IsFalse(
            viewModel.Message.IsError);

        Assert.AreEqual(
            "update_revocation_unavailable",
            viewModel.Message.Code);
    }

    [TestMethod]
    public void AcceptAutomaticResult_RetryLater_IsAWarningWithTheRetryMessage()
    {
        UpdateViewModel viewModel =
            CreateInitializedViewModel(
                new FakeUpdateCoordinator());

        viewModel.AcceptAutomaticResult(
            new AutomaticUpdateResult
            {
                Action =
                    AutomaticUpdateAction.RetryLater,

                Check =
                    CreateCheckResult(
                        UpdateCheckStatus.UpdateAvailable,
                        CreateCandidate(),
                        scope: InstallScope.CurrentUser),

                Preparation =
                    new UpdatePreparationResult
                    {
                        Status =
                            UpdatePreparationStatus
                                .VerificationDeferred,

                        Verification =
                            DeferredInstallResult.Verification
                    }
            });

        Assert.IsTrue(
            viewModel.Message!.IsWarning);

        Assert.IsTrue(
            viewModel.Message.Text.Contains(
                "try again later"),
            viewModel.Message.Text);

        Assert.IsNull(
            viewModel.PreparedPackage);
    }

    private static UpdateViewModel
        CreateInitializedViewModel(
            FakeUpdateCoordinator coordinator)
    {
        UpdateViewModel viewModel =
            new(
                coordinator);

        viewModel.Initialize(
            "2.0.0");

        return viewModel;
    }

    private static UpdateCheckResult CreateCheckResult(
        UpdateCheckStatus status,
        UpdateCandidate? candidate,
        InstallChannel channel =
            InstallChannel.Msi,
        InstallScope scope =
            InstallScope.AllUsers)
    {
        return new UpdateCheckResult
        {
            Status =
                status,

            Installation =
                new InstallationContext
                {
                    Channel =
                        channel,

                    Scope =
                        scope
                },

            State =
                new UpdateState
                {
                    LastCheckedAtUtc =
                        ReferenceTime,

                    LastAvailableVersion =
                        candidate?.AvailableVersion
                },

            ResolvedMode =
                UpdateMode.DownloadAndPrompt,

            Candidate =
                candidate
        };
    }

    private static UpdateCandidate CreateCandidate(
        bool isRequired = false)
    {
        return new UpdateCandidate
        {
            CurrentVersion =
                "2.0.0",

            Manifest =
                CreateManifest(),

            IsRequired =
                isRequired
        };
    }

    private static UpdateManifest CreateManifest()
    {
        return new UpdateManifest
        {
            Version =
                "2.1.0",

            Channel =
                "stable",

            AssetName =
                "CopyGIF-2.1.0-x64.msi",

            AssetUri =
                new Uri(
                    "https://github.com/hphifer99/CopyGIF/releases/download/v2.1.0/CopyGIF-2.1.0-x64.msi"),

            SizeBytes =
                1024,

            Sha256 =
                new string(
                    'a',
                    64),

            MinimumSupportedVersion =
                "2.0.0",

            ReleaseNotesUri =
                new Uri(
                    "https://github.com/hphifer99/CopyGIF/releases/tag/v2.1.0"),

            PublishedAtUtc =
                ReferenceTime
        };
    }

    private static DownloadedUpdatePackage CreatePackage(
        UpdateManifest manifest)
    {
        return new DownloadedUpdatePackage
        {
            Manifest =
                manifest,

            FilePath =
                @"C:\CopyGIF\Updates\CopyGIF-2.1.0-x64.msi",

            SizeBytes =
                manifest.SizeBytes,

            Sha256 =
                manifest.Sha256,

            DownloadedAtUtc =
                ReferenceTime
        };
    }

    private sealed class FakeUpdateCoordinator :
        IUpdateCoordinator
    {
        public UpdateCheckResult CheckResult
        {
            get;
            init;
        } =
            CreateCheckResult(
                UpdateCheckStatus.NoUpdateAvailable,
                candidate: null);

        public UpdatePreparationResult PreparationResult
        {
            get;
            init;
        } =
            new()
            {
                Status =
                    UpdatePreparationStatus
                        .VerificationFailed,

                Verification =
                    UpdatePackageVerificationResult.Invalid(
                        UpdatePackageVerificationFailure.Unknown,
                        "No preparation result was configured.")
            };

        public UpdateInstallationResult InstallationResult
        {
            get;
            init;
        } =
            new()
            {
                Status =
                    UpdateInstallationStatus
                        .VerificationFailed,

                Verification =
                    UpdatePackageVerificationResult.Invalid(
                        UpdatePackageVerificationFailure.Unknown,
                        "No installation result was configured.")
            };

        public AutomaticUpdateResult AutomaticResult
        {
            get;
            init;
        } =
            new()
            {
                Action =
                    AutomaticUpdateAction.None,

                Check =
                    CreateCheckResult(
                        UpdateCheckStatus.NoUpdateAvailable,
                        candidate: null)
            };

        public bool BlockCheckUntilCancelled
        {
            get;
            init;
        }

        public TaskCompletionSource<bool>
            CheckStarted
        { get; } =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        public int CheckCount
        {
            get;
            private set;
        }

        public int PrepareCount
        {
            get;
            private set;
        }

        public int InstallCount
        {
            get;
            private set;
        }

        public int AutomaticCount
        {
            get;
            private set;
        }

        public string? LastCurrentVersion
        {
            get;
            private set;
        }

        public bool LastForce
        {
            get;
            private set;
        }

        public UpdateCandidate? LastPreparedCandidate
        {
            get;
            private set;
        }

        public DownloadedUpdatePackage?
            LastInstalledPackage
        {
            get;
            private set;
        }

        public bool PrepareReceivedProgress
        {
            get;
            private set;
        }

        public async Task<UpdateCheckResult> CheckAsync(
            string currentVersion,
            bool force = false,
            CancellationToken cancellationToken = default)
        {
            CheckCount++;

            LastCurrentVersion =
                currentVersion;

            LastForce =
                force;

            CheckStarted.TrySetResult(
                true);

            if (BlockCheckUntilCancelled)
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken);
            }

            cancellationToken
                .ThrowIfCancellationRequested();

            return CheckResult;
        }

        public Task<UpdatePreparationResult> PrepareAsync(
            UpdateCandidate candidate,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            PrepareCount++;

            LastPreparedCandidate =
                candidate;

            PrepareReceivedProgress =
                progress is not null;

            progress?.Report(
                new UpdateDownloadProgress
                {
                    BytesReceived =
                        candidate.Manifest.SizeBytes,

                    TotalBytes =
                        candidate.Manifest.SizeBytes
                });

            return Task.FromResult(
                PreparationResult);
        }

        public Task<UpdateInstallationResult> InstallAsync(
            DownloadedUpdatePackage package,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            InstallCount++;

            LastInstalledPackage =
                package;

            return Task.FromResult(
                InstallationResult);
        }

        public UpdateInstallOptions? LastInstallOptions
        {
            get;
            private set;
        }

        public string? LastSkippedVersion
        {
            get;
            private set;
        }

        public Task<UpdateInstallationResult> InstallAsync(
            DownloadedUpdatePackage package,
            UpdateInstallOptions options,
            CancellationToken cancellationToken = default)
        {
            LastInstallOptions =
                options;

            return InstallAsync(
                package,
                cancellationToken);
        }

        public Task SkipVersionAsync(
            string version,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastSkippedVersion =
                version;

            return Task.CompletedTask;
        }

        public Task<bool> DeferInstallToNextLaunchAsync(
            DownloadedUpdatePackage package,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                false);

        public Task<PendingUpdateInstallResult> InstallPendingAsync(
            string currentVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new PendingUpdateInstallResult
                {
                    Status =
                        PendingUpdateInstallStatus
                            .NothingPending
                });

        public Task<AutomaticUpdateResult> RunAutomaticAsync(
            string currentVersion,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            AutomaticCount++;

            LastCurrentVersion =
                currentVersion;

            return Task.FromResult(
                AutomaticResult);
        }
    }
}
