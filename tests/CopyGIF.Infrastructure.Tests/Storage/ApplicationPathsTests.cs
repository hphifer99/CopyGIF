using CopyGIF.Core.Policies;
using CopyGIF.Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Infrastructure.Tests.Storage;

[TestClass]
public sealed class ApplicationPathsTests
{
    private string _testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "CopyGIF.Tests",
                Guid.NewGuid().ToString("N"));
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
    public void Constructor_UsesCanonicalRootPath()
    {
        string pathWithSegments =
            Path.Combine(
                _testDirectory,
                "First",
                "..",
                "Profile");

        ApplicationPaths paths =
            new(pathWithSegments);

        string expected =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    Path.Combine(
                        _testDirectory,
                        "Profile")));

        Assert.AreEqual(
            expected,
            paths.RootDirectory);
    }

    [TestMethod]
    public void Paths_MatchFrozenProfileLayout()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        Assert.AreEqual(
            Path.Combine(
                paths.RootDirectory,
                StoragePolicy.SettingsFileName),
            paths.SettingsPath);

        Assert.AreEqual(
            paths.SettingsPath + ".bak",
            paths.SettingsBackupPath);

        Assert.AreEqual(
            Path.Combine(
                paths.RootDirectory,
                StoragePolicy.LibraryFileName),
            paths.LibraryPath);

        Assert.AreEqual(
            paths.LibraryPath + ".bak",
            paths.LibraryBackupPath);

        Assert.AreEqual(
            Path.Combine(
                paths.RootDirectory,
                StoragePolicy.SearchHistoryFileName),
            paths.SearchHistoryPath);

        Assert.AreEqual(
            paths.SearchHistoryPath + ".bak",
            paths.SearchHistoryBackupPath);

        Assert.AreEqual(
            Path.Combine(
                paths.RootDirectory,
                StoragePolicy.UpdateStateFileName),
            paths.UpdateStatePath);

        Assert.AreEqual(
            paths.UpdateStatePath + ".bak",
            paths.UpdateStateBackupPath);

        Assert.AreEqual(
            Path.Combine(
                paths.RootDirectory,
                StoragePolicy.MigrationStateFileName),
            paths.MigrationStatePath);

        Assert.AreEqual(
            paths.MigrationStatePath + ".bak",
            paths.MigrationStateBackupPath);
    }

    [TestMethod]
    public void CachePaths_MatchFrozenCacheLayout()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        Assert.AreEqual(
            Path.Combine(
                paths.RootDirectory,
                StoragePolicy.CacheDirectoryName),
            paths.CacheDirectory);

        Assert.AreEqual(
            Path.Combine(
                paths.CacheDirectory,
                StoragePolicy.ThumbnailCacheDirectoryName),
            paths.ThumbnailCacheDirectory);

        Assert.AreEqual(
            Path.Combine(
                paths.CacheDirectory,
                StoragePolicy.PreviewCacheDirectoryName),
            paths.PreviewCacheDirectory);

        Assert.AreEqual(
            Path.Combine(
                paths.CacheDirectory,
                StoragePolicy.ClipboardCacheDirectoryName),
            paths.ClipboardCacheDirectory);
    }

    [TestMethod]
    public void CustomLibraryRoot_AppendsCopyGifDirectory()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        string customRoot =
            Path.Combine(
                _testDirectory,
                "ExternalDrive");

        string expectedLibraryRoot =
            Path.Combine(
                Path.GetFullPath(
                    customRoot),
                StoragePolicy.LibraryRootDirectoryName);

        Assert.AreEqual(
            expectedLibraryRoot,
            paths.GetLibraryRoot(
                customRoot));

        Assert.AreEqual(
            Path.Combine(
                expectedLibraryRoot,
                StoragePolicy.FavoritesDirectoryName),
            paths.GetFavoritesDirectory(
                customRoot));

        Assert.AreEqual(
            Path.Combine(
                expectedLibraryRoot,
                StoragePolicy.RecentsDirectoryName),
            paths.GetRecentsDirectory(
                customRoot));
    }

    [TestMethod]
    public void NullCustomRoot_UsesProfileRoot()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        Assert.AreEqual(
            paths.RootDirectory,
            paths.GetLibraryRoot(
                customStorageRoot: null));

        Assert.AreEqual(
            paths.FavoritesDirectory,
            paths.GetFavoritesDirectory(
                customStorageRoot: null));

        Assert.AreEqual(
            paths.RecentsDirectory,
            paths.GetRecentsDirectory(
                customStorageRoot: null));
    }

    [TestMethod]
    public void WhitespaceCustomRoot_IsRejected()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        Assert.ThrowsExactly<
            ArgumentException>(
            () =>
                paths.GetLibraryRoot(
                    "   "));
    }

    [TestMethod]
    public void EnsureDirectoriesExist_CreatesProfileLayout()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        paths.EnsureDirectoriesExist();

        string[] expectedDirectories =
        [
            paths.RootDirectory,
            paths.SecretsDirectory,
            paths.CacheDirectory,
            paths.ThumbnailCacheDirectory,
            paths.PreviewCacheDirectory,
            paths.ClipboardCacheDirectory,
            paths.UpdatesDirectory,
            paths.LogsDirectory,
            paths.MigrationDirectory,
            paths.FavoritesDirectory,
            paths.RecentsDirectory
        ];

        foreach (string directory
                 in expectedDirectories)
        {
            Assert.IsTrue(
                Directory.Exists(
                    directory),
                $"Expected directory was not created: {directory}");
        }
    }

    [TestMethod]
    public void EnsureLibraryDirectoriesExist_CreatesCustomLayout()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        string customRoot =
            Path.Combine(
                _testDirectory,
                "CustomStorage");

        paths.EnsureLibraryDirectoriesExist(
            customRoot);

        Assert.IsTrue(
            Directory.Exists(
                paths.GetLibraryRoot(
                    customRoot)));

        Assert.IsTrue(
            Directory.Exists(
                paths.GetFavoritesDirectory(
                    customRoot)));

        Assert.IsTrue(
            Directory.Exists(
                paths.GetRecentsDirectory(
                    customRoot)));
    }
}
