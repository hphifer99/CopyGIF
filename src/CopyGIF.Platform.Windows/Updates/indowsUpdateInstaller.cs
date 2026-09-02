using System.Security.Cryptography;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Platform.Windows.Updates;

public sealed class WindowsUpdateInstaller :
    IUpdateInstaller
{
    private const int Sha256ByteCount = 32;

    private readonly IAuthenticodeVerifier
        _authenticodeVerifier;

    private readonly IUpdatePackageLauncher
        _packageLauncher;

    public WindowsUpdateInstaller()
        : this(
            new WindowsAuthenticodeVerifier(),
            new WindowsMsiLauncher())
    {
    }

    internal WindowsUpdateInstaller(
        IAuthenticodeVerifier authenticodeVerifier,
        IUpdatePackageLauncher packageLauncher)
    {
        _authenticodeVerifier =
            authenticodeVerifier ??
            throw new ArgumentNullException(
                nameof(authenticodeVerifier));

        _packageLauncher =
            packageLauncher ??
            throw new ArgumentNullException(
                nameof(packageLauncher));
    }

    public async Task<
        UpdatePackageVerificationResult> VerifyAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            package);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            UpdatePackageVerificationResult?
                packageShapeFailure =
                    ValidatePackageShape(
                        package);

            if (packageShapeFailure is not null)
            {
                return packageShapeFailure;
            }

            string fullPath =
                Path.GetFullPath(
                    package.FilePath);

            if (!File.Exists(fullPath))
            {
                return UpdatePackageVerificationResult
                    .Invalid(
                        UpdatePackageVerificationFailure
                            .FileMissing,
                        "The downloaded update package is missing.");
            }

            FileInfo fileInfo =
                new(fullPath);

            if (fileInfo.Length !=
                    package.Manifest.SizeBytes ||
                fileInfo.Length !=
                    package.SizeBytes)
            {
                return UpdatePackageVerificationResult
                    .Invalid(
                        UpdatePackageVerificationFailure
                            .SizeMismatch,
                        "The downloaded update package size does not match the signed manifest.");
            }

            if (!TryParseSha256(
                    package.Manifest.Sha256,
                    out byte[] manifestHash) ||
                !TryParseSha256(
                    package.Sha256,
                    out byte[] packageHash) ||
                !CryptographicOperations
                    .FixedTimeEquals(
                        manifestHash,
                        packageHash))
            {
                return UpdatePackageVerificationResult
                    .Invalid(
                        UpdatePackageVerificationFailure
                            .HashMismatch,
                        "The downloaded update package hash does not match the signed manifest.");
            }

            await using FileStream stream =
                new(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    useAsync: true);

            byte[] actualHash =
                await SHA256.HashDataAsync(
                    stream,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!CryptographicOperations
                    .FixedTimeEquals(
                        manifestHash,
                        actualHash))
            {
                return UpdatePackageVerificationResult
                    .Invalid(
                        UpdatePackageVerificationFailure
                            .HashMismatch,
                        "The downloaded update package failed its SHA-256 integrity check.");
            }

            AuthenticodeVerificationStatus
                signatureStatus =
                    _authenticodeVerifier.Verify(
                        fullPath);

            return signatureStatus switch
            {
                AuthenticodeVerificationStatus.Trusted =>
                    UpdatePackageVerificationResult.Valid(),

                AuthenticodeVerificationStatus
                    .UntrustedPublisher =>
                    UpdatePackageVerificationResult
                        .Invalid(
                            UpdatePackageVerificationFailure
                                .UntrustedPublisher,
                            "The update package was not signed by the installed CopyGIF publisher."),

                _ =>
                    UpdatePackageVerificationResult
                        .Invalid(
                            UpdatePackageVerificationFailure
                                .InvalidSignature,
                            "Windows could not verify the update package signature.")
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  CryptographicException or
                  ArgumentException or
                  NotSupportedException)
        {
            return UpdatePackageVerificationResult
                .Invalid(
                    UpdatePackageVerificationFailure
                        .Unknown,
                    "Windows could not read the downloaded update package.");
        }
    }

    public async Task InstallAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default)
    {
        UpdatePackageVerificationResult verification =
            await VerifyAsync(
                package,
                cancellationToken)
            .ConfigureAwait(false);

        if (!verification.IsValid)
        {
            throw new InvalidOperationException(
                verification.Message ??
                "The update package could not be verified.");
        }

        await _packageLauncher.LaunchAsync(
                Path.GetFullPath(
                    package.FilePath),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static UpdatePackageVerificationResult?
        ValidatePackageShape(
            DownloadedUpdatePackage package)
    {
        if (string.IsNullOrWhiteSpace(
                package.FilePath) ||
            package.Manifest.SchemaVersion !=
                UpdateManifest.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(
                package.Manifest.AssetName) ||
            !string.Equals(
                Path.GetFileName(
                    package.Manifest.AssetName),
                package.Manifest.AssetName,
                StringComparison.Ordinal) ||
            !string.Equals(
                Path.GetExtension(
                    package.Manifest.AssetName),
                ".msi",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Path.GetExtension(
                    package.FilePath),
                ".msi",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Path.GetFileName(
                    package.FilePath),
                package.Manifest.AssetName,
                StringComparison.OrdinalIgnoreCase))
        {
            return UpdatePackageVerificationResult
                .Invalid(
                    UpdatePackageVerificationFailure
                        .UnsupportedPackage,
                    "The downloaded update is not the expected Windows Installer package.");
        }

        if (package.Manifest.SizeBytes < 0 ||
            package.SizeBytes < 0)
        {
            return UpdatePackageVerificationResult
                .Invalid(
                    UpdatePackageVerificationFailure
                        .SizeMismatch,
                    "The downloaded update package has an invalid size.");
        }

        return null;
    }

    private static bool TryParseSha256(
        string? value,
        out byte[] hash)
    {
        hash = [];

        if (string.IsNullOrWhiteSpace(
                value) ||
            value.Length != Sha256ByteCount * 2)
        {
            return false;
        }

        try
        {
            hash = Convert.FromHexString(
                value);

            return hash.Length ==
                Sha256ByteCount;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
