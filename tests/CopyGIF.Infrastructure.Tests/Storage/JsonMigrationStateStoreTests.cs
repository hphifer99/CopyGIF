using CopyGIF.Core.Models;
using CopyGIF.Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Infrastructure.Tests.Storage;

[TestClass]
public sealed class JsonMigrationStateStoreTests
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
    public async Task SaveThenLoadAsync_RoundTripsCompletionMarker()
    {
        (JsonMigrationStateStore store, _) =
            CreateStore();

        DateTimeOffset completedAt =
            new(
                2026,
                9,
                1,
                12,
                0,
                0,
                TimeSpan.Zero);

        await store.SaveAsync(
            new MigrationState
            {
                IsCompleted = true,
                CompletedAtUtc = completedAt,
                SourceVersion = "1.0.0"
            });

        MigrationState loaded =
            await store.LoadAsync();

        Assert.IsTrue(
            loaded.IsCompleted);

        Assert.AreEqual(
            completedAt,
            loaded.CompletedAtUtc);

        Assert.AreEqual(
            "1.0.0",
            loaded.SourceVersion);
    }

    [TestMethod]
    public async Task SaveAsync_CompletedWithoutTimestamp_IsRejected()
    {
        (JsonMigrationStateStore store, _) =
            CreateStore();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.SaveAsync(
                new MigrationState
                {
                    IsCompleted = true
                }));
    }

    [TestMethod]
    public async Task LoadAsync_CorruptState_PreservesItAndReturnsIncompleteState()
    {
        (JsonMigrationStateStore store, ApplicationPaths paths) =
            CreateStore();

        await File.WriteAllTextAsync(
            paths.MigrationStatePath,
            "{ corrupt migration state");

        MigrationState loaded =
            await store.LoadAsync();

        Assert.IsFalse(
            loaded.IsCompleted);

        Assert.IsTrue(
            File.Exists(
                paths.MigrationStatePath));

        Assert.AreEqual(
            1,
            Directory.GetFiles(
                _testDirectory,
                "migration-state.json.corrupt.*").Length);
    }

    private (JsonMigrationStateStore Store, ApplicationPaths Paths)
        CreateStore()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        JsonMigrationStateStore store =
            new(
                paths,
                new VersionedJsonSerializer(
                    new AtomicFileWriter(),
                    new CorruptFileRecovery()));

        return (store, paths);
    }
}
