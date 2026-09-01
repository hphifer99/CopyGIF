using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IUpdateInstaller
{
    Task<UpdatePackageVerificationResult> VerifyAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default);

    Task InstallAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default);
}
