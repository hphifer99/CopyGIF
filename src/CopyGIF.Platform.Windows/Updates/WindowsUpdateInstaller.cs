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

    // After an elevated installer has been started, the verified package stays locked for a
    // short while, so it cannot be swapped before the installer has opened it. The wait is
    // only for a per-machine installation, where the installer runs with more rights than
    // CopyGIF. A per-user installer runs as the same user and crosses no privilege boundary.
    internal static readonly TimeSpan
        DefaultElevatedHandoffSettleTime =
            TimeSpan.FromSeconds(5);

    private readonly IUpdatePackageLauncher
        _packageLauncher;

    private readonly TimeSpan
        _elevatedHandoffSettleTime;

    public WindowsUpdateInstaller()
        : this(
            new WindowsAuthenticodeVerifier(),
            new WindowsMsiLauncher())
    {
    }

    internal WindowsUpdateInstaller(
        IAuthenticodeVerifier authenticodeVerifier,
        IUpdatePackageLauncher packageLauncher,
        TimeSpan? elevatedHandoffSettleTime = null)
    {
        _elevatedHandoffSettleTime =
            elevatedHandoffSettleTime ??
            DefaultElevatedHandoffSettleTime;

        _authenticodeVerifier =
            authenticodeVerifier ??
            throw new ArgumentNullException(
                nameof(authenticodeVerifier));

        _packageLauncher =
            packageLauncher ??
            throw new ArgumentNullException(
                nameof(packageLauncher));
    }

    public Task<UpdatePackageVerificationResult> VerifyAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default) =>
        VerifyAsync(
            package,
            UpdateVerificationOptions.Full,
            cancellationToken);

    public async Task<
        UpdatePackageVerificationResult> VerifyAsync(
        DownloadedUpdatePackage package,
        UpdateVerificationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            package);

        ArgumentNullException.ThrowIfNull(
            options);

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
                        fullPath,
                        options.CheckRevocationOnline);

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

                AuthenticodeVerificationStatus
                    .RevocationUnavailable =>
                    UpdatePackageVerificationResult
                        .Invalid(
                            UpdatePackageVerificationFailure
                                .RevocationCheckUnavailable,
                            "Windows could not confirm that the update signing certificate is still valid. It will be checked again later."),

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

    public Task InstallAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default) =>
        InstallAsync(
            package,
            UpdateInstallOptions.Interactive,
            cancellationToken);

    public async Task InstallAsync(
        DownloadedUpdatePackage package,
        UpdateInstallOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            package);

        ArgumentNullException.ThrowIfNull(
            options);

        UpdatePackageVerificationResult?
            packageShapeFailure =
                ValidatePackageShape(
                    package);

        if (packageShapeFailure is not null)
        {
            throw new InvalidOperationException(
                packageShapeFailure.Message ??
                "The update package could not be verified.");
        }

        string fullPath =
            Path.GetFullPath(
                package.FilePath);

        // Keep a read-only handle open from verification through process
        // launch. FileShare.Read prevents another process from replacing,
        // deleting, or modifying the verified MSI during the handoff.
        await using FileStream installationLock =
            new(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 1,
                useAsync: true);

        UpdatePackageVerificationResult verification =
            await VerifyAsync(
                    package,
                    new UpdateVerificationOptions
                    {
                        CheckRevocationOnline =
                            options.CheckRevocationOnline
                    },
                    cancellationToken)
                .ConfigureAwait(false);

        if (!verification.IsValid)
        {
            throw new InvalidOperationException(
                verification.Message ??
                "The update package could not be verified.");
        }

        await _packageLauncher.LaunchAsync(
                fullPath,
                options,
                cancellationToken)
            .ConfigureAwait(false);

        if (options.RequiresElevation &&
            _elevatedHandoffSettleTime > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(
                        _elevatedHandoffSettleTime,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // The installer is already running. Cancelling only ends the extra wait.
            }
        }
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
