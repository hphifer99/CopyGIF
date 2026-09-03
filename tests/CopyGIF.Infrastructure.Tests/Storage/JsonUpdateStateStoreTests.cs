using CopyGIF.Core.Models;
using CopyGIF.Infrastructure.Storage;

namespace CopyGIF.Infrastructure.Tests.Storage;

[TestClass]
public sealed class JsonUpdateStateStoreTests
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
    public async Task SaveThenLoadAsync_RoundTripsUpdateState()
    {
        (JsonUpdateStateStore store, _) =
            CreateStore();

        DateTimeOffset checkedAt =
            new(
                2026,
                9,
                3,
                12,
                0,
                0,
                TimeSpan.Zero);

        DateTimeOffset downloadedAt =
            checkedAt.AddMinutes(
                2);

        await store.SaveAsync(
            new UpdateState
            {
                LastCheckedAtUtc =
                    checkedAt,

                LastAvailableVersion =
                    "2.0.1",

                LastDownloadedVersion =
                    "2.0.1",

                LastDownloadedAtUtc =
                    downloadedAt
            });

        UpdateState loaded =
            await store.LoadAsync();

        Assert.AreEqual(
            checkedAt,
            loaded.LastCheckedAtUtc);

        Assert.AreEqual(
            "2.0.1",
            loaded.LastAvailableVersion);

        Assert.AreEqual(
            "2.0.1",
            loaded.LastDownloadedVersion);

        Assert.AreEqual(
            downloadedAt,
            loaded.LastDownloadedAtUtc);
    }

    [TestMethod]
    public async Task SaveAsync_IncompleteDownloadState_IsRejected()
    {
        (JsonUpdateStateStore store, _) =
            CreateStore();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.SaveAsync(
                new UpdateState
                {
                    LastDownloadedVersion =
                        "2.0.1"
                }));
    }

    [TestMethod]
    public async Task SaveAsync_NonUtcTimestamp_IsRejected()
    {
        (JsonUpdateStateStore store, _) =
            CreateStore();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.SaveAsync(
                new UpdateState
                {
                    LastCheckedAtUtc =
                        new DateTimeOffset(
                            2026,
                            9,
                            3,
                            12,
                            0,
                            0,
                            TimeSpan.FromHours(
                                -5))
                }));
    }

    [TestMethod]
    public async Task LoadAsync_CorruptState_PreservesItAndReturnsDefaults()
    {
        (JsonUpdateStateStore store, ApplicationPaths paths) =
            CreateStore();

        await File.WriteAllTextAsync(
            paths.UpdateStatePath,
            "{ corrupt update state");

        UpdateState loaded =
            await store.LoadAsync();

        Assert.IsFalse(
            loaded.HasCompletedCheck);

        Assert.IsTrue(
            File.Exists(
                paths.UpdateStatePath));

        Assert.AreEqual(
            1,
            Directory.GetFiles(
                _testDirectory,
                "update-state.json.corrupt.*").Length);
    }

    private (JsonUpdateStateStore Store, ApplicationPaths Paths)
        CreateStore()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        JsonUpdateStateStore store =
            new(
                paths,
                new VersionedJsonSerializer(
                    new AtomicFileWriter(),
                    new CorruptFileRecovery()));

        return (store, paths);
    }
}
