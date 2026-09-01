namespace CopyGIF.Core.Models;

public sealed record UpdateCandidate
{
    public required string CurrentVersion { get; init; }

    public required UpdateManifest Manifest { get; init; }

    public bool IsRequired { get; init; }

    public string AvailableVersion =>
        Manifest.Version;

    public Uri ReleaseNotesUri =>
        Manifest.ReleaseNotesUri;
}
