using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IUpdatePackageService
{
    Task<DownloadedUpdatePackage> DownloadAsync(
        UpdateManifest manifest,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default);
}
