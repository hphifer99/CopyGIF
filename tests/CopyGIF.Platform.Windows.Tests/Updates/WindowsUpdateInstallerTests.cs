using System.Security.Cryptography;
using CopyGIF.Core.Models;
using CopyGIF.Platform.Windows.Updates;

namespace CopyGIF.Platform.Windows.Tests.Updates;

[TestClass]
public sealed class WindowsUpdateInstallerTests
{
    private string _testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "CopyGIF.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            _testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(
                _testDirectory))
        {
            Directory.Delete(
                _testDirectory,
                recursive: true);
        }
    }

    [TestMethod]
    public async Task VerifyAsync_ValidPackage_ReturnsValid()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        WindowsUpdateInstaller installer =
            CreateInstaller(
                AuthenticodeVerificationStatus
                    .Trusted);

        UpdatePackageVerificationResult result =
            await installer.VerifyAsync(
                package);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(
            UpdatePackageVerificationFailure.None,
            result.Failure);
    }

    [TestMethod]
    public async Task VerifyAsync_MissingFile_ReturnsFileMissing()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        File.Delete(package.FilePath);

        WindowsUpdateInstaller installer =
            CreateInstaller(
                AuthenticodeVerificationStatus
                    .Trusted);

        UpdatePackageVerificationResult result =
            await installer.VerifyAsync(
                package);

        Assert.AreEqual(
            UpdatePackageVerificationFailure
                .FileMissing,
            result.Failure);
    }

    [TestMethod]
    public async Task VerifyAsync_UnexpectedFileName_ReturnsUnsupportedPackage()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        package = package with
        {
            Manifest = package.Manifest with
            {
                AssetName = "Different.msi"
            }
        };

        WindowsUpdateInstaller installer =
            CreateInstaller(
                AuthenticodeVerificationStatus
                    .Trusted);

        UpdatePackageVerificationResult result =
            await installer.VerifyAsync(
                package);

        Assert.AreEqual(
            UpdatePackageVerificationFailure
                .UnsupportedPackage,
            result.Failure);
    }

    [TestMethod]
    public async Task VerifyAsync_SizeMismatch_ReturnsSizeMismatch()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        package = package with
        {
            SizeBytes = package.SizeBytes + 1
        };

        WindowsUpdateInstaller installer =
            CreateInstaller(
                AuthenticodeVerificationStatus
                    .Trusted);

        UpdatePackageVerificationResult result =
            await installer.VerifyAsync(
                package);

        Assert.AreEqual(
            UpdatePackageVerificationFailure
                .SizeMismatch,
            result.Failure);
    }

    [TestMethod]
    public async Task VerifyAsync_HashMismatch_ReturnsHashMismatch()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        package = package with
        {
            Sha256 = new string('0', 64)
        };

        WindowsUpdateInstaller installer =
            CreateInstaller(
                AuthenticodeVerificationStatus
                    .Trusted);

        UpdatePackageVerificationResult result =
            await installer.VerifyAsync(
                package);

        Assert.AreEqual(
            UpdatePackageVerificationFailure
                .HashMismatch,
            result.Failure);
    }

    [TestMethod]
    public async Task VerifyAsync_TamperedFile_ReturnsHashMismatch()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        byte[] tamperedContent =
            await File.ReadAllBytesAsync(
                package.FilePath);

        tamperedContent[0] ^= 0xFF;

        await File.WriteAllBytesAsync(
            package.FilePath,
            tamperedContent);

        WindowsUpdateInstaller installer =
            CreateInstaller(
                AuthenticodeVerificationStatus
                    .Trusted);

        UpdatePackageVerificationResult result =
            await installer.VerifyAsync(
                package);

        Assert.AreEqual(
            UpdatePackageVerificationFailure
                .HashMismatch,
            result.Failure);
    }

    [TestMethod]
    public async Task VerifyAsync_InvalidSignature_ReturnsInvalidSignature()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        WindowsUpdateInstaller installer =
            CreateInstaller(
                AuthenticodeVerificationStatus
                    .InvalidSignature);

        UpdatePackageVerificationResult result =
            await installer.VerifyAsync(
                package);

        Assert.AreEqual(
            UpdatePackageVerificationFailure
                .InvalidSignature,
            result.Failure);
    }

    [TestMethod]
    public async Task VerifyAsync_UntrustedPublisher_ReturnsUntrustedPublisher()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        WindowsUpdateInstaller installer =
            CreateInstaller(
                AuthenticodeVerificationStatus
                    .UntrustedPublisher);

        UpdatePackageVerificationResult result =
            await installer.VerifyAsync(
                package);

        Assert.AreEqual(
            UpdatePackageVerificationFailure
                .UntrustedPublisher,
            result.Failure);
    }

    [TestMethod]
    public async Task InstallAsync_InvalidPackage_DoesNotLaunch()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        FakePackageLauncher launcher = new();

        WindowsUpdateInstaller installer =
            new(
                new FakeAuthenticodeVerifier(
                    AuthenticodeVerificationStatus
                        .InvalidSignature),
                launcher);

        await Assert.ThrowsExactlyAsync<
            InvalidOperationException>(
                () => installer.InstallAsync(
                    package));

        Assert.AreEqual(
            0,
            launcher.LaunchCount);
    }

    [TestMethod]
    public async Task InstallAsync_ValidPackage_LaunchesOnce()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        FakePackageLauncher launcher = new();

        WindowsUpdateInstaller installer =
            new(
                new FakeAuthenticodeVerifier(
                    AuthenticodeVerificationStatus
                        .Trusted),
                launcher);

        await installer.InstallAsync(
            package);

        Assert.AreEqual(
            1,
            launcher.LaunchCount);

        Assert.AreEqual(
            Path.GetFullPath(
                package.FilePath),
            launcher.LastPackagePath);
    }

    private static WindowsUpdateInstaller CreateInstaller(
        AuthenticodeVerificationStatus status)
    {
        return new WindowsUpdateInstaller(
            new FakeAuthenticodeVerifier(
                status),
            new FakePackageLauncher());
    }

    private async Task<DownloadedUpdatePackage>
        CreatePackageAsync()
    {
        byte[] content =
            "CopyGIF test update package"u8
                .ToArray();

        string filePath = Path.Combine(
            _testDirectory,
            "CopyGIF-2.0.0.msi");

        await File.WriteAllBytesAsync(
            filePath,
            content);

        string sha256 =
            Convert.ToHexString(
                SHA256.HashData(
                    content));

        UpdateManifest manifest = new()
        {
            Version = "2.0.0",
            Channel = "Stable",
            AssetName =
                Path.GetFileName(
                    filePath),
            AssetUri =
                new Uri(
                    "https://example.invalid/CopyGIF-2.0.0.msi"),
            SizeBytes = content.LongLength,
            Sha256 = sha256,
            MinimumSupportedVersion = "2.0.0",
            ReleaseNotesUri =
                new Uri(
                    "https://example.invalid/releases/2.0.0"),
            PublishedAtUtc =
                DateTimeOffset.UtcNow
        };

        return new DownloadedUpdatePackage
        {
            Manifest = manifest,
            FilePath = filePath,
            SizeBytes = content.LongLength,
            Sha256 = sha256,
            DownloadedAtUtc =
                DateTimeOffset.UtcNow
        };
    }

    private sealed class FakeAuthenticodeVerifier(
        AuthenticodeVerificationStatus status) :
        IAuthenticodeVerifier
    {
        public AuthenticodeVerificationStatus Verify(
            string filePath)
        {
            return status;
        }
    }

    private sealed class FakePackageLauncher :
        IUpdatePackageLauncher
    {
        public int LaunchCount { get; private set; }

        public string? LastPackagePath
        { get; private set; }

        public Task LaunchAsync(
            string packagePath,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LaunchCount++;
            LastPackagePath = packagePath;

            return Task.CompletedTask;
        }
    }
}
