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
                launcher,
                TimeSpan.Zero);

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
                launcher,
                TimeSpan.Zero);

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

    [TestMethod]
    public async Task InstallAsync_WithRestartOptions_PassesTheOptionsToTheLauncher()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        FakePackageLauncher launcher = new();

        WindowsUpdateInstaller installer =
            new(
                new FakeAuthenticodeVerifier(
                    AuthenticodeVerificationStatus
                        .Trusted),
                launcher,
                TimeSpan.Zero);

        UpdateInstallOptions options =
            new()
            {
                Silent = true,
                RestartApplication = true
            };

        await installer.InstallAsync(
            package,
            options);

        Assert.AreEqual(
            1,
            launcher.LaunchCount);

        Assert.AreSame(
            options,
            launcher.LastOptions);
    }

    [TestMethod]
    public async Task InstallAsync_WithoutOptions_UsesTheInteractiveDefault()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        FakePackageLauncher launcher = new();

        WindowsUpdateInstaller installer =
            new(
                new FakeAuthenticodeVerifier(
                    AuthenticodeVerificationStatus
                        .Trusted),
                launcher,
                TimeSpan.Zero);

        await installer.InstallAsync(
            package);

        Assert.AreSame(
            UpdateInstallOptions.Interactive,
            launcher.LastOptions);
    }

    [TestMethod]
    public async Task InstallAsync_InvalidPackageWithRestartOptions_DoesNotLaunch()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        FakePackageLauncher launcher = new();

        WindowsUpdateInstaller installer =
            new(
                new FakeAuthenticodeVerifier(
                    AuthenticodeVerificationStatus
                        .UntrustedPublisher),
                launcher,
                TimeSpan.Zero);

        await Assert.ThrowsExactlyAsync<
            InvalidOperationException>(
                () => installer.InstallAsync(
                    package,
                    new UpdateInstallOptions
                    {
                        Silent = true,
                        RestartApplication = true
                    }));

        Assert.AreEqual(
            0,
            launcher.LaunchCount);
    }

    [TestMethod]
    public async Task VerifyAsync_RevocationUnavailable_ReturnsRevocationCheckUnavailable()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        WindowsUpdateInstaller installer =
            CreateInstaller(
                AuthenticodeVerificationStatus
                    .RevocationUnavailable);

        UpdatePackageVerificationResult result =
            await installer.VerifyAsync(
                package);

        Assert.IsFalse(result.IsValid);

        Assert.AreEqual(
            UpdatePackageVerificationFailure
                .RevocationCheckUnavailable,
            result.Failure);
    }

    [TestMethod]
    public async Task VerifyAsync_Default_ChecksRevocationOnline()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        FakeAuthenticodeVerifier verifier =
            new(
                AuthenticodeVerificationStatus
                    .Trusted);

        WindowsUpdateInstaller installer =
            new(
                verifier,
                new FakePackageLauncher(),
                TimeSpan.Zero);

        await installer.VerifyAsync(
            package);

        Assert.AreEqual(
            true,
            verifier.LastCheckRevocationOnline);
    }

    [TestMethod]
    public async Task VerifyAsync_WithoutOnlineRevocation_TellsTheVerifier()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        FakeAuthenticodeVerifier verifier =
            new(
                AuthenticodeVerificationStatus
                    .Trusted);

        WindowsUpdateInstaller installer =
            new(
                verifier,
                new FakePackageLauncher(),
                TimeSpan.Zero);

        UpdatePackageVerificationResult result =
            await installer.VerifyAsync(
                package,
                new UpdateVerificationOptions
                {
                    CheckRevocationOnline = false
                });

        Assert.IsTrue(result.IsValid);

        Assert.AreEqual(
            false,
            verifier.LastCheckRevocationOnline);
    }

    [TestMethod]
    public async Task InstallAsync_WithoutOnlineRevocation_TellsTheVerifierAndStillLaunches()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        FakeAuthenticodeVerifier verifier =
            new(
                AuthenticodeVerificationStatus
                    .Trusted);

        FakePackageLauncher launcher = new();

        WindowsUpdateInstaller installer =
            new(
                verifier,
                launcher,
                TimeSpan.Zero);

        await installer.InstallAsync(
            package,
            new UpdateInstallOptions
            {
                Silent = true,
                RestartApplication = true,
                RequiresElevation = false,
                CheckRevocationOnline = false
            });

        Assert.AreEqual(
            false,
            verifier.LastCheckRevocationOnline);

        Assert.AreEqual(
            1,
            launcher.LaunchCount);
    }

    [TestMethod]
    public async Task InstallAsync_Elevated_KeepsTheVerifiedFileLockedForTheSettleTime()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        TimeSpan settle =
            TimeSpan.FromMilliseconds(600);

        WindowsUpdateInstaller installer =
            new(
                new FakeAuthenticodeVerifier(
                    AuthenticodeVerificationStatus
                        .Trusted),
                new FakePackageLauncher(),
                settle);

        System.Diagnostics.Stopwatch clock =
            System.Diagnostics.Stopwatch.StartNew();

        Task install =
            installer.InstallAsync(
                package,
                new UpdateInstallOptions
                {
                    RequiresElevation = true
                });

        // The launcher fake returns at once, so the install is still in its settle wait.
        await Task.Delay(
            100);

        Assert.IsFalse(
            install.IsCompleted);

        // Another writer must not get at the verified package during that wait.
        Assert.ThrowsExactly<IOException>(
            () =>
            {
                using FileStream writer =
                    new(
                        package.FilePath,
                        FileMode.Open,
                        FileAccess.Write,
                        FileShare.None);
            });

        await install;

        Assert.IsTrue(
            clock.Elapsed >=
            settle - TimeSpan.FromMilliseconds(50));
    }

    [TestMethod]
    public async Task InstallAsync_PerUser_DoesNotWaitAfterTheLaunch()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        WindowsUpdateInstaller installer =
            new(
                new FakeAuthenticodeVerifier(
                    AuthenticodeVerificationStatus
                        .Trusted),
                new FakePackageLauncher(),
                TimeSpan.FromSeconds(30));

        System.Diagnostics.Stopwatch clock =
            System.Diagnostics.Stopwatch.StartNew();

        await installer.InstallAsync(
            package,
            new UpdateInstallOptions
            {
                RequiresElevation = false
            });

        Assert.IsTrue(
            clock.Elapsed <
            TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    public async Task InstallAsync_ElevatedAndCancelledDuringTheSettleWait_StillCompletes()
    {
        DownloadedUpdatePackage package =
            await CreatePackageAsync();

        WindowsUpdateInstaller installer =
            new(
                new FakeAuthenticodeVerifier(
                    AuthenticodeVerificationStatus
                        .Trusted),
                new FakePackageLauncher(),
                TimeSpan.FromSeconds(30));

        using CancellationTokenSource cancellation =
            new(
                TimeSpan.FromMilliseconds(100));

        // The installer is already running, so cancelling only ends the extra wait.
        await installer.InstallAsync(
            package,
            new UpdateInstallOptions
            {
                RequiresElevation = true
            },
            cancellation.Token);
    }

    private static WindowsUpdateInstaller CreateInstaller(
        AuthenticodeVerificationStatus status)
    {
        return new WindowsUpdateInstaller(
            new FakeAuthenticodeVerifier(
                status),
            new FakePackageLauncher(),
            TimeSpan.Zero);
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
        public bool? LastCheckRevocationOnline
        { get; private set; }

        public AuthenticodeVerificationStatus Verify(
            string filePath)
        {
            LastCheckRevocationOnline = true;

            return status;
        }

        public AuthenticodeVerificationStatus Verify(
            string filePath,
            bool checkRevocationOnline)
        {
            LastCheckRevocationOnline =
                checkRevocationOnline;

            return status;
        }
    }

    private sealed class FakePackageLauncher :
        IUpdatePackageLauncher
    {
        public int LaunchCount { get; private set; }

        public string? LastPackagePath
        { get; private set; }

        public UpdateInstallOptions? LastOptions
        { get; private set; }

        public Task LaunchAsync(
            string packagePath,
            CancellationToken cancellationToken) =>
            LaunchAsync(
                packagePath,
                UpdateInstallOptions.Interactive,
                cancellationToken);

        public Task LaunchAsync(
            string packagePath,
            UpdateInstallOptions options,
            CancellationToken cancellationToken)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LaunchCount++;
            LastPackagePath = packagePath;
            LastOptions = options;

            return Task.CompletedTask;
        }
    }
}
