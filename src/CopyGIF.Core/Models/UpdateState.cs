namespace CopyGIF.Core.Models;

public sealed record UpdateState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public DateTimeOffset? LastCheckedAtUtc { get; init; }

    public string? LastAvailableVersion { get; init; }

    public string? LastDownloadedVersion { get; init; }

    public DateTimeOffset? LastDownloadedAtUtc { get; init; }

    // The one version the user chose to skip. Automatic checks stop downloading and
    // prompting for exactly this version; a newer version, a required update, or an
    // explicit manual check still surfaces normally.
    public string? SkippedVersion { get; init; }

    // A downloaded, verified update the user declined for now. CopyGIF installs it
    // automatically the next time it starts, once. It is cleared before that attempt.
    public PendingUpdateInstall? PendingInstall { get; init; }

    public bool HasCompletedCheck =>
        LastCheckedAtUtc is not null;

    public bool HasDownloadedUpdate =>
        LastDownloadedVersion is not null &&
        LastDownloadedAtUtc is not null;
}
