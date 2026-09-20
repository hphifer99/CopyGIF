namespace CopyGIF.Core.Models;

public sealed record UpdateManifest
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public required string Version { get; init; }

    public required string Channel { get; init; }

    public required string AssetName { get; init; }

    public required Uri AssetUri { get; init; }

    public required long SizeBytes { get; init; }

    public required string Sha256 { get; init; }

    public required string MinimumSupportedVersion { get; init; }

    public required Uri ReleaseNotesUri { get; init; }

    public required DateTimeOffset PublishedAtUtc { get; init; }
}
