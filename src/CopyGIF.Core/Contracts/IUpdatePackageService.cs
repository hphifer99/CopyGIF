using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IUpdatePackageService
{
    Task<DownloadedUpdatePackage> DownloadAsync(
        UpdateManifest manifest,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a package that was downloaded earlier for exactly this manifest, without using
    /// the network. Returns null when the file is missing or its size or SHA-256 hash no
    /// longer matches the manifest. The signature is not checked here.
    /// </summary>
    Task<DownloadedUpdatePackage?> FindExistingAsync(
        UpdateManifest manifest,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes installers for the running version or older, and abandoned partial
    /// downloads, from the CopyGIF-owned updates folder. Newer packages are never touched.
    /// </summary>
    Task PruneAsync(
        string currentVersion,
        CancellationToken cancellationToken = default);
}
