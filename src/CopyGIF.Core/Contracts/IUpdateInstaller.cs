using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IUpdateInstaller
{
    Task<UpdatePackageVerificationResult> VerifyAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default);

    Task<UpdatePackageVerificationResult> VerifyAsync(
        DownloadedUpdatePackage package,
        UpdateVerificationOptions options,
        CancellationToken cancellationToken = default);

    Task InstallAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default);

    Task InstallAsync(
        DownloadedUpdatePackage package,
        UpdateInstallOptions options,
        CancellationToken cancellationToken = default);
}
