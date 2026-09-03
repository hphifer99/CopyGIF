using CopyGIF.Core.Models;
using CopyGIF.Infrastructure.Storage;

namespace CopyGIF.Infrastructure.Tests.Storage;

[TestClass]
public sealed class LibraryStorageMoverTests
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
        try
        {
            if (Directory.Exists(
                    _testDirectory))
            {
                Directory.Delete(
                    _testDirectory,
                    recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [TestMethod]
    public async Task MoveAsync_CopiesFilesAndPreservesRelativeFolders()
    {
        string sourceRoot =
            Path.Combine(
                _testDirectory,
                "Source",
                "CopyGIF");

        string destinationRoot =
            Path.Combine(
                _testDirectory,
                "Destination",
                "CopyGIF");

        string sourceFile =
            Path.Combine(
                sourceRoot,
                "Favorites",
                "klipy-gif-123.gif");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                sourceFile)!);

        byte[] content =
            [
                (byte)'G',
                (byte)'I',
                (byte)'F',
                (byte)'8',
                (byte)'9',
                (byte)'a'
            ];

        await File.WriteAllBytesAsync(
            sourceFile,
            content);

        LibraryStorageMover mover =
            new(
                new OwnedPathGuard());

        CopyGIF.Core.Contracts.LibraryStorageMoveResult result =
            await mover.MoveAsync(
                sourceRoot,
                destinationRoot,
                [sourceFile]);

        string destinationFile =
            Path.Combine(
                destinationRoot,
                "Favorites",
                "klipy-gif-123.gif");

        Assert.IsFalse(
            File.Exists(
                sourceFile));

        Assert.IsTrue(
            File.Exists(
                destinationFile));

        CollectionAssert.AreEqual(
            content,
            await File.ReadAllBytesAsync(
                destinationFile));

        Assert.AreEqual(
            destinationFile,
            result.MovedPaths[
                Path.GetFullPath(
                    sourceFile)]);

        Assert.AreEqual(
            0,
            result.SourceFilesNotDeleted.Count);
    }

    [TestMethod]
    public async Task MoveAsync_MissingFile_IsIgnored()
    {
        string sourceRoot =
            Path.Combine(
                _testDirectory,
                "Source",
                "CopyGIF");

        string destinationRoot =
            Path.Combine(
                _testDirectory,
                "Destination",
                "CopyGIF");

        string missingFile =
            Path.Combine(
                sourceRoot,
                "Recents",
                "missing.gif");

        LibraryStorageMover mover =
            new(
                new OwnedPathGuard());

        CopyGIF.Core.Contracts.LibraryStorageMoveResult result =
            await mover.MoveAsync(
                sourceRoot,
                destinationRoot,
                [missingFile]);

        Assert.AreEqual(
            0,
            result.MovedPaths.Count);
    }

    [TestMethod]
    public async Task MoveAsync_OutsideSourceRoot_IsRejected()
    {
        string sourceRoot =
            Path.Combine(
                _testDirectory,
                "Source",
                "CopyGIF");

        string destinationRoot =
            Path.Combine(
                _testDirectory,
                "Destination",
                "CopyGIF");

        string outsideFile =
            Path.Combine(
                _testDirectory,
                "outside.gif");

        await File.WriteAllTextAsync(
            outsideFile,
            "GIF89a");

        LibraryStorageMover mover =
            new(
                new OwnedPathGuard());

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => mover.MoveAsync(
                    sourceRoot,
                    destinationRoot,
                    [outsideFile]));

        Assert.AreEqual(
            MediaDownloadFailure.UnsafePath,
            exception.Failure);
    }

    [TestMethod]
    public async Task MoveAsync_DestinationCollision_RollsBackEarlierCopies()
    {
        string sourceRoot =
            Path.Combine(
                _testDirectory,
                "Source",
                "CopyGIF");

        string destinationRoot =
            Path.Combine(
                _testDirectory,
                "Destination",
                "CopyGIF");

        string firstSource =
            Path.Combine(
                sourceRoot,
                "Favorites",
                "first.gif");

        string secondSource =
            Path.Combine(
                sourceRoot,
                "Favorites",
                "second.gif");

        string secondDestination =
            Path.Combine(
                destinationRoot,
                "Favorites",
                "second.gif");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                firstSource)!);

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                secondDestination)!);

        await File.WriteAllTextAsync(
            firstSource,
            "GIF89a-first");

        await File.WriteAllTextAsync(
            secondSource,
            "GIF89a-second");

        await File.WriteAllTextAsync(
            secondDestination,
            "existing");

        LibraryStorageMover mover =
            new(
                new OwnedPathGuard());

        await Assert.ThrowsAsync<IOException>(
            () => mover.MoveAsync(
                sourceRoot,
                destinationRoot,
                [
                    firstSource,
                    secondSource
                ]));

        Assert.IsTrue(
            File.Exists(
                firstSource));

        Assert.IsTrue(
            File.Exists(
                secondSource));

        Assert.IsFalse(
            File.Exists(
                Path.Combine(
                    destinationRoot,
                    "Favorites",
                    "first.gif")));

        Assert.AreEqual(
            "existing",
            await File.ReadAllTextAsync(
                secondDestination));
    }

    [TestMethod]
    public async Task DeleteAsync_DeletesOnlyOwnedFiles()
    {
        string ownedRoot =
            Path.Combine(
                _testDirectory,
                "CopyGIF");

        string ownedFile =
            Path.Combine(
                ownedRoot,
                "Recents",
                "owned.gif");

        string outsideFile =
            Path.Combine(
                _testDirectory,
                "outside.gif");

        Directory.CreateDirectory(
            Path.GetDirectoryName(
                ownedFile)!);

        await File.WriteAllTextAsync(
            ownedFile,
            "GIF89a");

        await File.WriteAllTextAsync(
            outsideFile,
            "GIF89a");

        LibraryStorageMover mover =
            new(
                new OwnedPathGuard());

        await mover.DeleteAsync(
            ownedRoot,
            [ownedFile]);

        Assert.IsFalse(
            File.Exists(
                ownedFile));

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => mover.DeleteAsync(
                    ownedRoot,
                    [outsideFile]));

        Assert.AreEqual(
            MediaDownloadFailure.UnsafePath,
            exception.Failure);

        Assert.IsTrue(
            File.Exists(
                outsideFile));
    }
}
