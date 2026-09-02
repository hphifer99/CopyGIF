using CopyGIF.Core.Models;
using CopyGIF.Infrastructure.Storage;

namespace CopyGIF.Infrastructure.Tests.Storage;

[TestClass]
public sealed class OwnedPathGuardTests
{
    private string _testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "CopyGIF.Tests",
                Guid.NewGuid()
                    .ToString("N"));

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
    public void EnsureSafeDirectory_OwnedChild_CreatesEveryDirectory()
    {
        string root =
            Path.Combine(
                _testDirectory,
                "Root");

        string child =
            Path.Combine(
                root,
                "Cache",
                "Preview");

        new OwnedPathGuard()
            .EnsureSafeDirectory(
                root,
                child);

        Assert.IsTrue(
            Directory.Exists(
                child));
    }

    [TestMethod]
    public void EnsureSafeDirectory_OutsideOwnedRoot_IsRejected()
    {
        string root =
            Path.Combine(
                _testDirectory,
                "Root");

        string outside =
            Path.Combine(
                _testDirectory,
                "Outside");

        MediaDownloadException exception =
            Assert.ThrowsExactly<
                MediaDownloadException>(
                () => new OwnedPathGuard()
                    .EnsureSafeDirectory(
                        root,
                        outside));

        Assert.AreEqual(
            MediaDownloadFailure.UnsafePath,
            exception.Failure);
    }

    [TestMethod]
    public void EnsureSafeFilePath_PrefixCollision_IsRejected()
    {
        string root =
            Path.Combine(
                _testDirectory,
                "Cache");

        string collision =
            Path.Combine(
                _testDirectory,
                "Cache-Evil",
                "preview.gif");

        MediaDownloadException exception =
            Assert.ThrowsExactly<
                MediaDownloadException>(
                () => new OwnedPathGuard()
                    .EnsureSafeFilePath(
                        root,
                        collision));

        Assert.AreEqual(
            MediaDownloadFailure.UnsafePath,
            exception.Failure);
    }

    [TestMethod]
    public void EnsureSafeDirectory_ReparsePointComponent_IsRejected()
    {
        string root =
            Path.Combine(
                _testDirectory,
                "Root");

        string outside =
            Path.Combine(
                _testDirectory,
                "Outside");

        string link =
            Path.Combine(
                root,
                "Linked");

        Directory.CreateDirectory(
            root);

        Directory.CreateDirectory(
            outside);

        try
        {
            Directory.CreateSymbolicLink(
                link,
                outside);
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  PlatformNotSupportedException)
        {
            return;
        }

        MediaDownloadException rejected =
            Assert.ThrowsExactly<
                MediaDownloadException>(
                () => new OwnedPathGuard()
                    .EnsureSafeDirectory(
                        root,
                        Path.Combine(
                            link,
                            "Preview")));

        Assert.AreEqual(
            MediaDownloadFailure.UnsafePath,
            rejected.Failure);

        Assert.IsFalse(
            Directory.Exists(
                Path.Combine(
                    outside,
                    "Preview")),
            "The guard must reject a reparse point before creating outside it.");
    }
}
