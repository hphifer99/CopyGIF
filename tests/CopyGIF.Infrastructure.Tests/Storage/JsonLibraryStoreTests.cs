using CopyGIF.Core.Models;
using CopyGIF.Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Infrastructure.Tests.Storage;

[TestClass]
public sealed class JsonLibraryStoreTests
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

        Directory.CreateDirectory(
            _testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        TryDeleteDirectory(
            _testDirectory);
    }

    [TestMethod]
    public async Task LoadAsync_NoFile_ReturnsEmptySnapshot()
    {
        (JsonLibraryStore store, _) =
            CreateStore();

        LibrarySnapshot snapshot =
            await store.LoadAsync();

        Assert.AreEqual(
            0,
            snapshot.Favorites.Count);

        Assert.AreEqual(
            0,
            snapshot.Recents.Count);
    }

    [TestMethod]
    public async Task SaveThenLoadAsync_RoundTripsLibrary()
    {
        (JsonLibraryStore store, ApplicationPaths paths) =
            CreateStore();

        LibraryEntry favorite =
            CreateEntry(
                "favorite-1",
                new DateTimeOffset(
                    2026,
                    9,
                    1,
                    12,
                    0,
                    0,
                    TimeSpan.Zero));

        LibraryEntry recent =
            CreateEntry(
                "recent-1",
                new DateTimeOffset(
                    2026,
                    9,
                    1,
                    13,
                    0,
                    0,
                    TimeSpan.Zero));

        await store.SaveAsync(
            new LibrarySnapshot
            {
                Favorites = [favorite],
                Recents = [recent]
            });

        LibrarySnapshot loaded =
            await store.LoadAsync();

        Assert.IsTrue(
            File.Exists(
                paths.LibraryPath));

        Assert.AreEqual(
            "favorite-1",
            loaded.Favorites.Single()
                .Identity.Id);

        Assert.AreEqual(
            "recent-1",
            loaded.Recents.Single()
                .Identity.Id);
    }

    [TestMethod]
    public async Task LoadAsync_CorruptPrimary_UsesBackupAndPreservesCorruptFile()
    {
        (JsonLibraryStore store, ApplicationPaths paths) =
            CreateStore();

        await store.SaveAsync(
            new LibrarySnapshot
            {
                Favorites =
                [
                    CreateEntry(
                        "first",
                        DateTimeOffset.UtcNow)
                ]
            });

        await store.SaveAsync(
            new LibrarySnapshot
            {
                Favorites =
                [
                    CreateEntry(
                        "second",
                        DateTimeOffset.UtcNow)
                ]
            });

        await File.WriteAllTextAsync(
            paths.LibraryPath,
            "{ invalid library");

        LibrarySnapshot recovered =
            await store.LoadAsync();

        Assert.AreEqual(
            "first",
            recovered.Favorites.Single()
                .Identity.Id);

        Assert.AreEqual(
            1,
            Directory.GetFiles(
                _testDirectory,
                "library.json.corrupt.*").Length);
    }

    [TestMethod]
    public async Task SaveAsync_NonHttpsEntry_IsRejected()
    {
        (JsonLibraryStore store, _) =
            CreateStore();

        LibraryEntry invalid =
            CreateEntry(
                "invalid",
                DateTimeOffset.UtcNow) with
            {
                GifUri =
                    new Uri(
                        "http://example.test/file.gif")
            };

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.SaveAsync(
                new LibrarySnapshot
                {
                    Favorites = [invalid]
                }));
    }

    [TestMethod]
    public async Task LoadAsync_FutureSchema_ThrowsWithoutChangingFile()
    {
        (JsonLibraryStore store, ApplicationPaths paths) =
            CreateStore();

        const string futureJson =
            """
        {
          "schemaVersion": 999
        }
        """;

        await File.WriteAllTextAsync(
            paths.LibraryPath,
            futureJson);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.LoadAsync());

        Assert.AreEqual(
            futureJson,
            await File.ReadAllTextAsync(
                paths.LibraryPath));
    }

    [TestMethod]
    public async Task LoadAsync_OversizedFile_IsPreservedBeforeDefaultsAreCreated()
    {
        (JsonLibraryStore store, ApplicationPaths paths) =
            CreateStore();

        await using (
            FileStream stream =
                File.Create(
                    paths.LibraryPath))
        {
            stream.SetLength(
                CopyGIF.Core.Policies.StoragePolicy
                    .MaximumLibraryFileBytes +
                1);
        }

        LibrarySnapshot loaded =
            await store.LoadAsync();

        Assert.AreEqual(
            0,
            loaded.Favorites.Count);

        Assert.IsTrue(
            File.Exists(
                paths.LibraryPath));

        string preservedPath =
            Directory.GetFiles(
                _testDirectory,
                "library.json.corrupt.*")
                .Single();

        Assert.AreEqual(
            CopyGIF.Core.Policies.StoragePolicy
                .MaximumLibraryFileBytes +
            1,
            new FileInfo(
                preservedPath).Length);
    }

    private (JsonLibraryStore Store, ApplicationPaths Paths)
        CreateStore()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        JsonLibraryStore store =
            new(
                paths,
                new VersionedJsonSerializer(
                    new AtomicFileWriter(),
                    new CorruptFileRecovery()));

        return (store, paths);
    }

    private static LibraryEntry CreateEntry(
        string id,
        DateTimeOffset addedAtUtc)
    {
        return new LibraryEntry
        {
            Identity =
                new GifIdentity(
                    "klipy",
                    id),
            Title = id,
            GifUri =
                new Uri(
                    $"https://cdn.example.test/{id}.gif"),
            ThumbnailUri =
                new Uri(
                    $"https://cdn.example.test/{id}.jpg"),
            AddedAtUtc = addedAtUtc,
            Width = 320,
            Height = 240,
            SizeBytes = 1024,
            CopyCount = 1
        };
    }

    private static void TryDeleteDirectory(
        string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(
                    path,
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
}
