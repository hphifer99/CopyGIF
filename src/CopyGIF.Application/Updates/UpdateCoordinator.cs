using System.Globalization;
using System.Runtime.ExceptionServices;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Updates;

public sealed class UpdateCoordinator :
    IUpdateCoordinator,
    IDisposable
{
    private const string StableChannel = "stable";

    private const int MaximumVersionLength = 128;

    private readonly ISettingsStore _settingsStore;

    private readonly IUpdateStateStore _stateStore;

    private readonly IUpdateFeed _updateFeed;

    private readonly IUpdatePackageService _packageService;

    private readonly IUpdateInstaller _installer;

    private readonly IInstallChannelService
        _installChannelService;

    private readonly IClock _clock;

    private readonly SemaphoreSlim _gate =
        new(
            initialCount: 1,
            maxCount: 1);

    private bool _disposed;

    public UpdateCoordinator(
        ISettingsStore settingsStore,
        IUpdateStateStore stateStore,
        IUpdateFeed updateFeed,
        IUpdatePackageService packageService,
        IUpdateInstaller installer,
        IInstallChannelService installChannelService,
        IClock clock)
    {
        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        _stateStore =
            stateStore ??
            throw new ArgumentNullException(
                nameof(stateStore));

        _updateFeed =
            updateFeed ??
            throw new ArgumentNullException(
                nameof(updateFeed));

        _packageService =
            packageService ??
            throw new ArgumentNullException(
                nameof(packageService));

        _installer =
            installer ??
            throw new ArgumentNullException(
                nameof(installer));

        _installChannelService =
            installChannelService ??
            throw new ArgumentNullException(
                nameof(installChannelService));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));
    }

    public async Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await CheckCoreAsync(
                    currentVersion,
                    force,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UpdatePreparationResult> PrepareAsync(
        UpdateCandidate candidate,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            candidate);

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await PrepareCoreAsync(
                    candidate,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UpdateInstallationResult> InstallAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            package);

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await InstallCoreAsync(
                    package,
                    UpdateInstallOptions.Interactive,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UpdateInstallationResult> InstallAsync(
        DownloadedUpdatePackage package,
        UpdateInstallOptions options,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            package);

        ArgumentNullException.ThrowIfNull(
            options);

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await InstallCoreAsync(
                    package,
                    options,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SkipVersionAsync(
        string version,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            version);

        string skipped =
            version.Trim();

        if (skipped.Length > MaximumVersionLength)
        {
            throw new ArgumentException(
                "The version to skip is too long.",
                nameof(version));
        }

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            UpdateState state =
                await _stateStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            // Skipping a version also cancels a "Not now" for that same version, so a skipped
            // update is never installed by the next start.
            PendingUpdateInstall? pending =
                state.PendingInstall is not null &&
                string.Equals(
                    state.PendingInstall.Manifest.Version,
                    skipped,
                    StringComparison.OrdinalIgnoreCase)
                    ? null
                    : state.PendingInstall;

            await _stateStore
                .SaveAsync(
                    state with
                    {
                        SchemaVersion =
                            UpdateState.CurrentSchemaVersion,
                        SkippedVersion = skipped,
                        PendingInstall = pending
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeferInstallToNextLaunchAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            package);

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            InstallationContext installation =
                await _installChannelService
                    .GetCurrentAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            // Only a per-user installation can be installed at start without an
            // administrator (UAC) prompt appearing out of nowhere.
            if (!CanInstallAtNextLaunch(
                    installation))
            {
                return false;
            }

            UpdateState state =
                await _stateStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            await _stateStore
                .SaveAsync(
                    state with
                    {
                        SchemaVersion =
                            UpdateState.CurrentSchemaVersion,
                        PendingInstall =
                            new PendingUpdateInstall
                            {
                                Manifest = package.Manifest,
                                DeferredAtUtc = _clock.UtcNow
                            }
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PendingUpdateInstallResult>
        InstallPendingAsync(
            string currentVersion,
            CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            UpdateState state =
                await _stateStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            PendingUpdateInstall? pending =
                state.PendingInstall;

            if (pending is null)
            {
                return CreatePendingResult(
                    PendingUpdateInstallStatus.NothingPending,
                    null);
            }

            string pendingVersion =
                pending.Manifest.Version;

            // At most one automatic attempt per deferred update. The record is removed before
            // anything below can fail or hang, so a broken package cannot cause a start-up loop.
            await _stateStore
                .SaveAsync(
                    state with
                    {
                        SchemaVersion =
                            UpdateState.CurrentSchemaVersion,
                        PendingInstall = null
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (!IsNewerVersion(
                    pendingVersion,
                    currentVersion))
            {
                return CreatePendingResult(
                    PendingUpdateInstallStatus.AlreadyCurrent,
                    pendingVersion);
            }

            AppSettings settings =
                AppSettingsNormalizer.Normalize(
                    await _settingsStore
                        .LoadAsync(
                            cancellationToken)
                        .ConfigureAwait(false));

            InstallationContext installation =
                await _installChannelService
                    .GetCurrentAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            // The user may have turned updates off, or changed to notify-only, after choosing
            // "Not now". That choice wins.
            if (!settings.Updates.CheckForUpdates ||
                !CanInstallAtNextLaunch(
                    installation) ||
                UpdatePolicy.ResolveMode(
                    settings.Updates.Mode,
                    installation) ==
                UpdateMode.NotifyOnly ||
                string.Equals(
                    state.SkippedVersion,
                    pendingVersion,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CreatePendingResult(
                    PendingUpdateInstallStatus.NotApplicable,
                    pendingVersion);
            }

            DownloadedUpdatePackage? package =
                await _packageService
                    .FindExistingAsync(
                        pending.Manifest,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (package is null)
            {
                return CreatePendingResult(
                    PendingUpdateInstallStatus.PackageUnavailable,
                    pendingVersion);
            }

            // The package was fully verified, revocation included, when it was downloaded.
            // Starting CopyGIF must not wait for the network, so the final check here leaves
            // out the online revocation lookup. Everything else is checked again.
            UpdateInstallationResult installResult =
                await InstallCoreAsync(
                        package,
                        new UpdateInstallOptions
                        {
                            Silent = true,
                            RestartApplication = true,
                            CheckRevocationOnline = false
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

            return CreatePendingResult(
                installResult.Status switch
                {
                    UpdateInstallationStatus.Installed =>
                        PendingUpdateInstallStatus.Started,

                    UpdateInstallationStatus.ManagedExternally =>
                        PendingUpdateInstallStatus.NotApplicable,

                    UpdateInstallationStatus.VerificationDeferred =>
                        PendingUpdateInstallStatus.PackageUnavailable,

                    _ =>
                        PendingUpdateInstallStatus.VerificationFailed
                },
                pendingVersion);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static PendingUpdateInstallResult CreatePendingResult(
        PendingUpdateInstallStatus status,
        string? version)
    {
        return new PendingUpdateInstallResult
        {
            Status = status,
            Version = version
        };
    }

    private static bool CanInstallAtNextLaunch(
        InstallationContext installation)
    {
        return UpdatePolicy.UsesApplicationUpdater(
                   installation) &&
               installation.Scope ==
               InstallScope.CurrentUser;
    }

    private static bool IsNewerVersion(
        string candidateVersion,
        string currentVersion)
    {
        try
        {
            return SemanticVersion
                       .Parse(
                           candidateVersion,
                           nameof(candidateVersion))
                       .CompareTo(
                           SemanticVersion.Parse(
                               currentVersion,
                               nameof(currentVersion))) >
                   0;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private async Task ClearPendingInstallAsync()
    {
        try
        {
            UpdateState state =
                await _stateStore
                    .LoadAsync(
                        CancellationToken.None)
                    .ConfigureAwait(false);

            if (state.PendingInstall is null)
            {
                return;
            }

            await _stateStore
                .SaveAsync(
                    state with
                    {
                        PendingInstall = null
                    },
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Housekeeping. The installer was already started.
        }
    }

    public async Task<AutomaticUpdateResult>
        RunAutomaticAsync(
            string currentVersion,
            IProgress<UpdateDownloadProgress>? progress = null,
            CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await TryPruneAsync(
                    currentVersion,
                    cancellationToken)
                .ConfigureAwait(false);

            UpdateCheckResult check =
                await CheckCoreAsync(
                        currentVersion,
                        force: false,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!check.HasUpdate ||
                IsSkipped(
                    check))
            {
                return new AutomaticUpdateResult
                {
                    Action = AutomaticUpdateAction.None,
                    Check = check
                };
            }

            if (check.ResolvedMode ==
                UpdateMode.NotifyOnly)
            {
                return new AutomaticUpdateResult
                {
                    Action = AutomaticUpdateAction.Notify,
                    Check = check
                };
            }

            try
            {
                return await RunAutomaticPreparationAsync(
                        check,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The check time was saved before the download started. Clear it so
                // an interrupted or failed download is retried at the next opportunity
                // instead of waiting for the next daily or weekly interval.
                await ClearLastCheckAsync()
                    .ConfigureAwait(false);

                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AutomaticUpdateResult>
        RunAutomaticPreparationAsync(
            UpdateCheckResult check,
            IProgress<UpdateDownloadProgress>? progress,
            CancellationToken cancellationToken)
    {
        UpdatePreparationResult preparation =
            await PrepareCoreAsync(
                    check.Candidate!,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

        if (preparation.Status ==
            UpdatePreparationStatus.VerificationDeferred)
        {
            // The check time was saved before the download started. Clear it so the next
            // attempt is not pushed out to the next daily or weekly interval.
            await ClearLastCheckAsync()
                .ConfigureAwait(false);

            return new AutomaticUpdateResult
            {
                Action =
                    AutomaticUpdateAction.RetryLater,
                Check = check,
                Preparation = preparation
            };
        }

        if (!preparation.IsReady)
        {
            return new AutomaticUpdateResult
            {
                Action =
                    AutomaticUpdateAction
                        .VerificationFailed,
                Check = check,
                Preparation = preparation
            };
        }

        // The package is downloaded and verified. Installing it means closing and restarting
        // CopyGIF, and only the host application knows whether the user is looking at it.
        // So the coordinator never installs from the background timer. It reports a prepared
        // update, and the host decides using Check.ResolvedMode: DownloadAndInstall installs
        // when CopyGIF is hidden in the tray and asks when it is on screen, DownloadAndPrompt
        // only ever asks.
        return new AutomaticUpdateResult
        {
            Action = AutomaticUpdateAction.Prompt,
            Check = check,
            Preparation = preparation
        };
    }

    private static bool IsSkipped(
        UpdateCheckResult check)
    {
        UpdateCandidate? candidate =
            check.Candidate;

        // A required update can never be skipped.
        return candidate is not null &&
               !candidate.IsRequired &&
               string.Equals(
                   check.State.SkippedVersion,
                   candidate.AvailableVersion,
                   StringComparison.OrdinalIgnoreCase);
    }

    private async Task TryPruneAsync(
        string currentVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            await _packageService
                .PruneAsync(
                    currentVersion,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Housekeeping must never stop an update check.
        }
    }

    private async Task ClearLastCheckAsync()
    {
        try
        {
            UpdateState state =
                await _stateStore
                    .LoadAsync(
                        CancellationToken.None)
                    .ConfigureAwait(false);

            await _stateStore
                .SaveAsync(
                    state with
                    {
                        LastCheckedAtUtc = null
                    },
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The original failure is the one worth reporting.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }

    private async Task<UpdateCheckResult>
        CheckCoreAsync(
            string currentVersion,
            bool force,
            CancellationToken cancellationToken)
    {
        SemanticVersion installedVersion =
            SemanticVersion.Parse(
                currentVersion,
                nameof(currentVersion));

        AppSettings settings =
            AppSettingsNormalizer.Normalize(
                await _settingsStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false));

        InstallationContext installation =
            await _installChannelService
                .GetCurrentAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        UpdateState state =
            await _stateStore
                .LoadAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        UpdateMode resolvedMode =
            UpdatePolicy.ResolveMode(
                settings.Updates.Mode,
                installation);

        if (!force && !settings.Updates.CheckForUpdates)
        {
            return CreateCheckResult(
                UpdateCheckStatus.Disabled,
                installation,
                state,
                resolvedMode);
        }

        if (!UpdatePolicy.UsesApplicationUpdater(
                installation))
        {
            return CreateCheckResult(
                installation.Channel == InstallChannel.MicrosoftStore
                    ? UpdateCheckStatus.ManagedByStore : UpdateCheckStatus.UnsupportedInstallation,
                installation,
                state,
                resolvedMode);
        }

        DateTimeOffset now = _clock.UtcNow;

        if (!force &&
            !IsCheckDue(
                state,
                settings.Updates.CheckFrequency,
                now))
        {
            return CreateCheckResult(
                UpdateCheckStatus.NotDue,
                installation,
                state,
                resolvedMode);
        }

        UpdateManifest? manifest =
            await _updateFeed
                .GetLatestAsync(
                    StableChannel,
                    cancellationToken)
                .ConfigureAwait(false);

        if (manifest is null)
            return CreateCheckResult(UpdateCheckStatus.FeedUnavailable, installation, state, resolvedMode);

        UpdateCandidate? candidate = null;

        if (manifest is not null)
        {
            ValidateManifest(
                manifest);

            SemanticVersion availableVersion =
                SemanticVersion.Parse(
                    manifest.Version,
                    nameof(manifest.Version));

            if (availableVersion.CompareTo(
                    installedVersion) > 0)
            {
                SemanticVersion minimumVersion =
                    SemanticVersion.Parse(
                        manifest.MinimumSupportedVersion,
                        nameof(manifest.MinimumSupportedVersion));

                candidate =
                    new UpdateCandidate
                    {
                        CurrentVersion =
                            currentVersion.Trim(),
                        Manifest = manifest,
                        IsRequired =
                            installedVersion.CompareTo(
                                minimumVersion) < 0
                    };
            }
        }

        UpdateState updatedState =
            state with
            {
                SchemaVersion =
                    UpdateState.CurrentSchemaVersion,
                LastCheckedAtUtc = now,
                LastAvailableVersion =
                    candidate?.AvailableVersion
            };

        await _stateStore
            .SaveAsync(
                updatedState,
                cancellationToken)
            .ConfigureAwait(false);

        return new UpdateCheckResult
        {
            Status = candidate is null
                ? UpdateCheckStatus.NoUpdateAvailable
                : UpdateCheckStatus.UpdateAvailable,
            Installation = installation,
            State = updatedState,
            ResolvedMode = resolvedMode,
            Candidate = candidate
        };
    }

    private async Task<UpdatePreparationResult>
        PrepareCoreAsync(
            UpdateCandidate candidate,
            IProgress<UpdateDownloadProgress>? progress,
            CancellationToken cancellationToken)
    {
        ValidateCandidate(
            candidate);

        DownloadedUpdatePackage package =
            await _packageService
                .DownloadAsync(
                    candidate.Manifest,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

        UpdatePackageVerificationResult verification;

        if (!Equals(
                package.Manifest,
                candidate.Manifest))
        {
            verification =
                UpdatePackageVerificationResult.Invalid(
                    UpdatePackageVerificationFailure
                        .UnsupportedPackage,
                    "The downloaded update does not match the selected release manifest.");
        }
        else
        {
            try
            {
                verification =
                    await _installer
                        .VerifyAsync(
                            package,
                            cancellationToken)
                        .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await DeletePackageOrThrowAsync(
                        package,
                        exception)
                    .ConfigureAwait(false);

                ExceptionDispatchInfo
                    .Capture(
                        exception)
                    .Throw();

                throw;
            }
        }

        if (!verification.IsValid)
        {
            // A package that only could not be confirmed (revocation servers unreachable) is
            // not damaged, so it stays on disk and is checked again later.
            if (verification.Failure ==
                UpdatePackageVerificationFailure
                    .RevocationCheckUnavailable)
            {
                return new UpdatePreparationResult
                {
                    Status =
                        UpdatePreparationStatus
                            .VerificationDeferred,
                    Verification = verification
                };
            }

            await DeletePackageOrThrowAsync(
                    package,
                    originalException: null)
                .ConfigureAwait(false);

            return new UpdatePreparationResult
            {
                Status =
                    UpdatePreparationStatus
                        .VerificationFailed,
                Verification = verification
            };
        }

        try
        {
            UpdateState state =
                await _stateStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            UpdateState updatedState =
                state with
                {
                    SchemaVersion =
                        UpdateState.CurrentSchemaVersion,
                    LastDownloadedVersion =
                        candidate.AvailableVersion,
                    LastDownloadedAtUtc =
                        _clock.UtcNow
                };

            await _stateStore
                .SaveAsync(
                    updatedState,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await DeletePackageOrThrowAsync(
                    package,
                    exception)
                .ConfigureAwait(false);

            ExceptionDispatchInfo
                .Capture(
                    exception)
                .Throw();

            throw;
        }

        return new UpdatePreparationResult
        {
            Status = UpdatePreparationStatus.Ready,
            Verification = verification,
            Package = package
        };
    }

    private async Task<UpdateInstallationResult>
        InstallCoreAsync(
            DownloadedUpdatePackage package,
            UpdateInstallOptions options,
            CancellationToken cancellationToken)
    {
        InstallationContext installation =
            await _installChannelService
                .GetCurrentAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (!UpdatePolicy.UsesApplicationUpdater(
                installation))
        {
            return new UpdateInstallationResult
            {
                Status =
                    UpdateInstallationStatus
                        .ManagedExternally,
                Verification =
                    UpdatePackageVerificationResult.Valid()
            };
        }

        UpdatePackageVerificationResult verification;

        try
        {
            verification =
                await _installer
                    .VerifyAsync(
                        package,
                        new UpdateVerificationOptions
                        {
                            CheckRevocationOnline =
                                options.CheckRevocationOnline
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await DeletePackageOrThrowAsync(
                    package,
                    exception)
                .ConfigureAwait(false);

            ExceptionDispatchInfo
                .Capture(
                    exception)
                .Throw();

            throw;
        }

        if (!verification.IsValid &&
            verification.Failure ==
            UpdatePackageVerificationFailure
                .RevocationCheckUnavailable)
        {
            return new UpdateInstallationResult
            {
                Status =
                    UpdateInstallationStatus
                        .VerificationDeferred,
                Verification = verification
            };
        }

        if (!verification.IsValid)
        {
            await DeletePackageOrThrowAsync(
                    package,
                    originalException: null)
                .ConfigureAwait(false);

            return new UpdateInstallationResult
            {
                Status =
                    UpdateInstallationStatus
                        .VerificationFailed,
                Verification = verification
            };
        }

        // Only a per-machine installation needs the elevation (UAC) prompt.
        await _installer
            .InstallAsync(
                package,
                options with
                {
                    RequiresElevation =
                        installation.Scope !=
                        InstallScope.CurrentUser
                },
                cancellationToken)
            .ConfigureAwait(false);

        // The installer is running, so a remembered "Not now" is finished.
        await ClearPendingInstallAsync()
            .ConfigureAwait(false);

        return new UpdateInstallationResult
        {
            Status = UpdateInstallationStatus.Installed,
            Verification = verification
        };
    }

    private async Task DeletePackageOrThrowAsync(
        DownloadedUpdatePackage package,
        Exception? originalException)
    {
        try
        {
            await _packageService
                .DeleteAsync(
                    package,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception cleanupException)
        {
            if (originalException is null)
            {
                throw;
            }

            throw new AggregateException(
                "The update operation failed and the downloaded package could not be cleaned up.",
                originalException,
                cleanupException);
        }
    }

    private static UpdateCheckResult CreateCheckResult(
        UpdateCheckStatus status,
        InstallationContext installation,
        UpdateState state,
        UpdateMode resolvedMode)
    {
        return new UpdateCheckResult
        {
            Status = status,
            Installation = installation,
            State = state,
            ResolvedMode = resolvedMode
        };
    }

    private static bool IsCheckDue(
        UpdateState state,
        UpdateCheckFrequency frequency,
        DateTimeOffset now)
    {
        if (state.LastCheckedAtUtc is null)
        {
            return true;
        }

        TimeSpan interval =
            frequency switch
            {
                UpdateCheckFrequency.Daily =>
                    TimeSpan.FromDays(1),

                UpdateCheckFrequency.Weekly =>
                    TimeSpan.FromDays(7),

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(frequency),
                        frequency,
                        "The update-check frequency is not supported.")
            };

        return now - state.LastCheckedAtUtc.Value >=
            interval;
    }

    private static void ValidateCandidate(
        UpdateCandidate candidate)
    {
        SemanticVersion.Parse(
            candidate.CurrentVersion,
            nameof(candidate.CurrentVersion));

        ValidateManifest(
            candidate.Manifest);
    }

    private static void ValidateManifest(
        UpdateManifest manifest)
    {
        if (manifest.SchemaVersion !=
            UpdateManifest.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                "The update manifest schema is not supported.");
        }

        SemanticVersion availableVersion =
            SemanticVersion.Parse(
                manifest.Version,
                nameof(manifest.Version));

        SemanticVersion minimumVersion =
            SemanticVersion.Parse(
                manifest.MinimumSupportedVersion,
                nameof(manifest.MinimumSupportedVersion));

        if (minimumVersion.CompareTo(
                availableVersion) > 0)
        {
            throw new InvalidDataException(
                "The update manifest minimum supported version exceeds the release version.");
        }

        if (!string.Equals(
                manifest.Channel,
                StableChannel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The update manifest channel is not supported.");
        }

        if (string.IsNullOrWhiteSpace(
                manifest.AssetName) ||
            !string.Equals(
                manifest.AssetName,
                Path.GetFileName(
                    manifest.AssetName),
                StringComparison.Ordinal) ||
            !string.Equals(
                Path.GetExtension(
                    manifest.AssetName),
                ".msi",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The update manifest does not identify a valid MSI asset name.");
        }

        if (!IsPublicHttpsUri(
                manifest.AssetUri) ||
            !IsPublicHttpsUri(
                manifest.ReleaseNotesUri))
        {
            throw new InvalidDataException(
                "Update and release-note addresses must use public HTTPS URLs.");
        }

        if (manifest.SizeBytes <= 0)
        {
            throw new InvalidDataException(
                "The update manifest contains an invalid package size.");
        }

        if (!TryParseSha256(
                manifest.Sha256))
        {
            throw new InvalidDataException(
                "The update manifest contains an invalid SHA-256 value.");
        }
    }

    private static bool IsPublicHttpsUri(
        Uri? uri)
    {
        return uri is
        {
            IsAbsoluteUri: true,
            Scheme: "https"
        } &&
        !uri.IsLoopback &&
        !string.IsNullOrWhiteSpace(
            uri.Host);
    }

    private static bool TryParseSha256(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value) ||
            value.Length != 64)
        {
            return false;
        }

        try
        {
            return Convert.FromHexString(
                    value)
                .Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    private sealed class SemanticVersion :
        IComparable<SemanticVersion>
    {
        private readonly int[] _numbers;

        private readonly string[] _prerelease;

        private SemanticVersion(
            int[] numbers,
            string[] prerelease)
        {
            _numbers = numbers;
            _prerelease = prerelease;
        }

        public static SemanticVersion Parse(
            string? value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                throw new ArgumentException(
                    "A version is required.",
                    parameterName);
            }

            string normalized = value.Trim();

            if (normalized.Length > MaximumVersionLength)
            {
                throw new ArgumentException(
                    "The version is too long.",
                    parameterName);
            }

            if (normalized[0] is 'v' or 'V')
            {
                normalized = normalized[1..];
            }

            int buildMetadataIndex =
                normalized.IndexOf(
                    '+',
                    StringComparison.Ordinal);

            if (buildMetadataIndex >= 0)
            {
                normalized =
                    normalized[..buildMetadataIndex];
            }

            string[] versionAndPrerelease =
                normalized.Split(
                    '-',
                    count: 2,
                    StringSplitOptions.None);

            string[] numberParts =
                versionAndPrerelease[0].Split(
                    '.',
                    StringSplitOptions.None);

            if (numberParts.Length is < 1 or > 4)
            {
                throw new ArgumentException(
                    "The version must contain one to four numeric components.",
                    parameterName);
            }

            int[] numbers = new int[4];

            for (int index = 0;
                 index < numberParts.Length;
                 index++)
            {
                if (!int.TryParse(
                        numberParts[index],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out int number))
                {
                    throw new ArgumentException(
                        "The version contains an invalid numeric component.",
                        parameterName);
                }

                numbers[index] = number;
            }

            string[] prerelease =
                versionAndPrerelease.Length == 1
                    ? []
                    : versionAndPrerelease[1].Split(
                        '.',
                        StringSplitOptions.None);

            if (prerelease.Any(
                    identifier =>
                        string.IsNullOrWhiteSpace(
                            identifier) ||
                        identifier.Any(
                            character =>
                                !char.IsAsciiLetterOrDigit(
                                    character) &&
                                character != '-')))
            {
                throw new ArgumentException(
                    "The version contains an invalid prerelease identifier.",
                    parameterName);
            }

            return new SemanticVersion(
                numbers,
                prerelease);
        }

        public int CompareTo(
            SemanticVersion? other)
        {
            if (other is null)
            {
                return 1;
            }

            for (int index = 0;
                 index < _numbers.Length;
                 index++)
            {
                int numberComparison =
                    _numbers[index].CompareTo(
                        other._numbers[index]);

                if (numberComparison != 0)
                {
                    return numberComparison;
                }
            }

            if (_prerelease.Length == 0 ||
                other._prerelease.Length == 0)
            {
                return _prerelease.Length ==
                       other._prerelease.Length
                    ? 0
                    : _prerelease.Length == 0
                        ? 1
                        : -1;
            }

            int sharedLength =
                Math.Min(
                    _prerelease.Length,
                    other._prerelease.Length);

            for (int index = 0;
                 index < sharedLength;
                 index++)
            {
                int identifierComparison =
                    ComparePrereleaseIdentifier(
                        _prerelease[index],
                        other._prerelease[index]);

                if (identifierComparison != 0)
                {
                    return identifierComparison;
                }
            }

            return _prerelease.Length.CompareTo(
                other._prerelease.Length);
        }

        private static int ComparePrereleaseIdentifier(
            string left,
            string right)
        {
            bool leftIsNumber =
                int.TryParse(
                    left,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int leftNumber);

            bool rightIsNumber =
                int.TryParse(
                    right,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int rightNumber);

            if (leftIsNumber && rightIsNumber)
            {
                return leftNumber.CompareTo(
                    rightNumber);
            }

            if (leftIsNumber != rightIsNumber)
            {
                return leftIsNumber
                    ? -1
                    : 1;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(
                left,
                right);
        }
    }
}
