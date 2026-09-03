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
    UpdateAvailable
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
    VerificationFailed
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
    VerificationFailed
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
    VerificationFailed
}

public sealed record AutomaticUpdateResult
{
    public required AutomaticUpdateAction Action { get; init; }

    public required UpdateCheckResult Check { get; init; }

    public UpdatePreparationResult? Preparation { get; init; }

    public UpdateInstallationResult? Installation { get; init; }
}
