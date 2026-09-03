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

    public bool HasCompletedCheck =>
        LastCheckedAtUtc is not null;

    public bool HasDownloadedUpdate =>
        LastDownloadedVersion is not null &&
        LastDownloadedAtUtc is not null;
}
