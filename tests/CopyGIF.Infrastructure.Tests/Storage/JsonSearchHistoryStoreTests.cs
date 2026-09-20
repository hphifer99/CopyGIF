using CopyGIF.Core.Models;
using CopyGIF.Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Infrastructure.Tests.Storage;

[TestClass]
public sealed class JsonSearchHistoryStoreTests
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
    public async Task SaveThenLoadAsync_RoundTripsHistory()
    {
        (JsonSearchHistoryStore store, _) =
            CreateStore();

        DateTimeOffset usedAt =
            new(
                2026,
                9,
                1,
                12,
                0,
                0,
                TimeSpan.Zero);

        await store.SaveAsync(
            new SearchHistorySnapshot
            {
                Entries =
                [
                    new SearchHistoryEntry
                    {
                        Query = "celebration",
                        LastUsedAtUtc = usedAt,
                        UseCount = 3
                    }
                ]
            });

        SearchHistorySnapshot loaded =
            await store.LoadAsync();

        SearchHistoryEntry entry =
            loaded.Entries.Single();

        Assert.AreEqual(
            "celebration",
            entry.Query);

        Assert.AreEqual(
            usedAt,
            entry.LastUsedAtUtc);

        Assert.AreEqual(
            3,
            entry.UseCount);
    }

    [TestMethod]
    public async Task ClearAsync_RemovesHistoryFromPrimaryBackupAndCorruptCopies()
    {
        (JsonSearchHistoryStore store, ApplicationPaths paths) =
            CreateStore();

        SearchHistorySnapshot snapshot =
            new()
            {
                Entries =
                [
                    new SearchHistoryEntry
                    {
                        Query = "private search",
                        LastUsedAtUtc =
                            DateTimeOffset.UtcNow
                    }
                ]
            };

        await store.SaveAsync(snapshot);
        await store.SaveAsync(snapshot);

        await File.WriteAllTextAsync(
            paths.SearchHistoryPath +
            ".corrupt.test",
            "private search");

        await store.ClearAsync();

        SearchHistorySnapshot cleared =
            await store.LoadAsync();

        Assert.AreEqual(
            0,
            cleared.Entries.Count);

        Assert.IsFalse(
            File.Exists(
                paths.SearchHistoryBackupPath));

        Assert.AreEqual(
            0,
            Directory.GetFiles(
                _testDirectory,
                "search-history.json.corrupt.*").Length);
    }

    [TestMethod]
    public async Task LoadAsync_CorruptPrimary_UsesBackup()
    {
        (JsonSearchHistoryStore store, ApplicationPaths paths) =
            CreateStore();

        SearchHistorySnapshot first =
            CreateSnapshot(
                "first");

        SearchHistorySnapshot second =
            CreateSnapshot(
                "second");

        await store.SaveAsync(first);
        await store.SaveAsync(second);

        await File.WriteAllTextAsync(
            paths.SearchHistoryPath,
            "{ corrupt history");

        SearchHistorySnapshot recovered =
            await store.LoadAsync();

        Assert.AreEqual(
            "first",
            recovered.Entries.Single()
                .Query);
    }

    [TestMethod]
    public async Task SaveAsync_BlankQuery_IsRejected()
    {
        (JsonSearchHistoryStore store, _) =
            CreateStore();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.SaveAsync(
                new SearchHistorySnapshot
                {
                    Entries =
                    [
                        new SearchHistoryEntry
                        {
                            Query = "   ",
                            LastUsedAtUtc =
                                DateTimeOffset.UtcNow
                        }
                    ]
                }));
    }

    private (JsonSearchHistoryStore Store, ApplicationPaths Paths)
        CreateStore()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        JsonSearchHistoryStore store =
            new(
                paths,
                new VersionedJsonSerializer(
                    new AtomicFileWriter(),
                    new CorruptFileRecovery()));

        return (store, paths);
    }

    private static SearchHistorySnapshot CreateSnapshot(
        string query)
    {
        return new SearchHistorySnapshot
        {
            Entries =
            [
                new SearchHistoryEntry
                {
                    Query = query,
                    LastUsedAtUtc =
                        DateTimeOffset.UtcNow
                }
            ]
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
