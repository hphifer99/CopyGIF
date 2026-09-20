namespace CopyGIF.Core.Models;

/// <summary>
/// A downloaded and verified update that the user answered "Not now" to. CopyGIF installs it
/// automatically the next time it starts. The manifest is kept so the package on disk can be
/// found and checked again without asking the network.
/// </summary>
public sealed record PendingUpdateInstall
{
    public required UpdateManifest Manifest { get; init; }

    public required DateTimeOffset DeferredAtUtc { get; init; }
}
