using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Updates;

public interface IUpdateCoordinator
{
    Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        bool force = false,
        CancellationToken cancellationToken = default);

    Task<UpdatePreparationResult> PrepareAsync(
        UpdateCandidate candidate,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<UpdateInstallationResult> InstallAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default);

    Task<UpdateInstallationResult> InstallAsync(
        DownloadedUpdatePackage package,
        UpdateInstallOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remembers that the user does not want this version. Automatic checks then stop
    /// downloading and prompting for exactly this version. A newer version, a required
    /// update, and an explicit manual check are not affected.
    /// </summary>
    Task SkipVersionAsync(
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remembers that the user answered "Not now" to a prepared update, so CopyGIF installs it
    /// automatically the next time it starts. Returns false when that is not possible for this
    /// installation (only a per-user MSI installation can install without an administrator
    /// prompt), in which case nothing is remembered.
    /// </summary>
    Task<bool> DeferInstallToNextLaunchAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called once while CopyGIF starts, before any window exists. If the user deferred an
    /// update earlier, this checks the package again without waiting for the network and hands
    /// it to the installer, and the caller must then close CopyGIF at once. There is at most
    /// one automatic attempt per deferred update: the record is cleared before anything else
    /// can fail, so a broken package can never cause a start-up loop.
    /// </summary>
    Task<PendingUpdateInstallResult> InstallPendingAsync(
        string currentVersion,
        CancellationToken cancellationToken = default);

    Task<AutomaticUpdateResult> RunAutomaticAsync(
        string currentVersion,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public enum UpdateCheckStatus
{
    Disabled,
    ManagedByStore,
    NotDue,
    NoUpdateAvailable,
    UpdateAvailable,
    UnsupportedInstallation,
    FeedUnavailable
}

public sealed record UpdateCheckResult
{
    public required UpdateCheckStatus Status { get; init; }

    public required InstallationContext Installation { get; init; }

    public required UpdateState State { get; init; }

    public UpdateMode ResolvedMode { get; init; }

    public UpdateCandidate? Candidate { get; init; }

    public bool HasUpdate =>
        Status == UpdateCheckStatus.UpdateAvailable &&
        Candidate is not null;
}

public enum UpdatePreparationStatus
{
    Ready,
    VerificationFailed,

    // The package could not be confirmed right now because the certificate revocation servers
    // were unreachable. The package is kept and checked again later.
    VerificationDeferred
}

public sealed record UpdatePreparationResult
{
    public required UpdatePreparationStatus Status { get; init; }

    public required UpdatePackageVerificationResult Verification { get; init; }

    public DownloadedUpdatePackage? Package { get; init; }

    public bool IsReady =>
        Status == UpdatePreparationStatus.Ready &&
        Package is not null &&
        Verification.IsValid;
}

public enum UpdateInstallationStatus
{
    Installed,
    ManagedExternally,
    VerificationFailed,

    // See UpdatePreparationStatus.VerificationDeferred. The package is kept.
    VerificationDeferred
}

public sealed record UpdateInstallationResult
{
    public required UpdateInstallationStatus Status { get; init; }

    public required UpdatePackageVerificationResult Verification { get; init; }

    public bool WasInstalled =>
        Status == UpdateInstallationStatus.Installed;
}

public enum AutomaticUpdateAction
{
    None,
    Notify,
    Prompt,
    Installed,
    VerificationFailed,

    // The package could not be confirmed right now (certificate revocation servers
    // unreachable). Nothing is shown; the host tries again later.
    RetryLater
}

public sealed record AutomaticUpdateResult
{
    public required AutomaticUpdateAction Action { get; init; }

    public required UpdateCheckResult Check { get; init; }

    public UpdatePreparationResult? Preparation { get; init; }

    public UpdateInstallationResult? Installation { get; init; }
}

public enum PendingUpdateInstallStatus
{
    /// <summary>Nothing was deferred.</summary>
    NothingPending,

    /// <summary>The installer was started. The caller must close CopyGIF now.</summary>
    Started,

    /// <summary>CopyGIF is already at or past the deferred version.</summary>
    AlreadyCurrent,

    /// <summary>Automatic installation is not allowed now (updates off, notify only, wrong install type).</summary>
    NotApplicable,

    /// <summary>The package file is gone or no longer matches its manifest.</summary>
    PackageUnavailable,

    /// <summary>The package failed verification and was deleted.</summary>
    VerificationFailed
}

public sealed record PendingUpdateInstallResult
{
    public required PendingUpdateInstallStatus Status { get; init; }

    public string? Version { get; init; }

    public bool ShouldExit =>
        Status == PendingUpdateInstallStatus.Started;
}
