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
    public async Task SaveThenLoadAsync_RoundTripsSkippedVersion()
    {
        (JsonUpdateStateStore store, _) =
            CreateStore();

        await store.SaveAsync(
            new UpdateState
            {
                SkippedVersion = "2.1.0"
            });

        UpdateState loaded =
            await store.LoadAsync();

        Assert.AreEqual(
            "2.1.0",
            loaded.SkippedVersion);
    }

    [TestMethod]
    public async Task SaveThenLoadAsync_RoundTripsThePendingInstall()
    {
        (JsonUpdateStateStore store, _) =
            CreateStore();

        DateTimeOffset deferredAt =
            new(
                2026,
                9,
                3,
                12,
                0,
                0,
                TimeSpan.Zero);

        UpdateManifest manifest =
            CreateManifest();

        await store.SaveAsync(
            new UpdateState
            {
                SkippedVersion = "2.0.5",
                PendingInstall =
                    new PendingUpdateInstall
                    {
                        Manifest = manifest,
                        DeferredAtUtc = deferredAt
                    }
            });

        UpdateState loaded =
            await store.LoadAsync();

        Assert.IsNotNull(
            loaded.PendingInstall);

        Assert.AreEqual(
            manifest,
            loaded.PendingInstall!.Manifest);

        Assert.AreEqual(
            deferredAt,
            loaded.PendingInstall.DeferredAtUtc);

        Assert.AreEqual(
            "2.0.5",
            loaded.SkippedVersion);
    }

    [TestMethod]
    public async Task LoadAsync_StateFileWithoutAPendingInstall_LoadsWithNone()
    {
        // A state file written before "Not now" existed must still load.
        (JsonUpdateStateStore store, ApplicationPaths paths) =
            CreateStore();

        await File.WriteAllTextAsync(
            paths.UpdateStatePath,
            "{ \"schemaVersion\": 1, \"skippedVersion\": \"2.0.5\" }");

        UpdateState loaded =
            await store.LoadAsync();

        Assert.IsNull(
            loaded.PendingInstall);

        Assert.AreEqual(
            "2.0.5",
            loaded.SkippedVersion);
    }

    [TestMethod]
    public async Task SaveAsync_PendingInstallWithAnInvalidManifest_IsRejected()
    {
        (JsonUpdateStateStore store, _) =
            CreateStore();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.SaveAsync(
                new UpdateState
                {
                    PendingInstall =
                        new PendingUpdateInstall
                        {
                            Manifest =
                                CreateManifest() with
                                {
                                    AssetName =
                                        "..\\CopyGIF-2.1.0-x64.msi"
                                },
                            DeferredAtUtc =
                                DateTimeOffset.UtcNow
                        }
                }));
    }

    [TestMethod]
    public async Task SaveAsync_PendingInstallWithANonUtcTimestamp_IsRejected()
    {
        (JsonUpdateStateStore store, _) =
            CreateStore();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.SaveAsync(
                new UpdateState
                {
                    PendingInstall =
                        new PendingUpdateInstall
                        {
                            Manifest = CreateManifest(),
                            DeferredAtUtc =
                                new DateTimeOffset(
                                    2026,
                                    9,
                                    3,
                                    12,
                                    0,
                                    0,
                                    TimeSpan.FromHours(
                                        -5))
                        }
                }));
    }

    [TestMethod]
    public async Task LoadAsync_PendingInstallEditedToAnUnsafeAsset_PreservesTheFileAndReturnsDefaults()
    {
        // Someone (or something) rewrote the record to point at another file. It must not load.
        (JsonUpdateStateStore store, ApplicationPaths paths) =
            CreateStore();

        await store.SaveAsync(
            new UpdateState
            {
                PendingInstall =
                    new PendingUpdateInstall
                    {
                        Manifest = CreateManifest(),
                        DeferredAtUtc =
                            DateTimeOffset.UtcNow
                    }
            });

        string json =
            await File.ReadAllTextAsync(
                paths.UpdateStatePath);

        const string original =
            "\"assetName\": \"CopyGIF-2.1.0-x64.msi\"";

        Assert.IsTrue(
            json.Contains(
                original),
            json);

        await File.WriteAllTextAsync(
            paths.UpdateStatePath,
            json.Replace(
                original,
                "\"assetName\": \"..\\\\Evil.exe\""));

        UpdateState loaded =
            await store.LoadAsync();

        Assert.IsNull(
            loaded.PendingInstall);
    }

    [TestMethod]
    public async Task SaveAsync_SkippedVersionWithControlCharacter_IsRejected()
    {
        (JsonUpdateStateStore store, _) =
            CreateStore();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.SaveAsync(
                new UpdateState
                {
                    SkippedVersion = "2.1.0\n"
                }));
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

    private static UpdateManifest CreateManifest() =>
        new()
        {
            Version = "2.1.0",
            Channel = "stable",
            AssetName = "CopyGIF-2.1.0-x64.msi",
            AssetUri =
                new Uri(
                    "https://github.com/hphifer99/CopyGIF/releases/download/v2.1.0/CopyGIF-2.1.0-x64.msi"),
            SizeBytes = 1024,
            Sha256 =
                new string(
                    'a',
                    64),
            MinimumSupportedVersion = "2.0.0",
            ReleaseNotesUri =
                new Uri(
                    "https://github.com/hphifer99/CopyGIF/releases/tag/v2.1.0"),
            PublishedAtUtc =
                new DateTimeOffset(
                    2026,
                    9,
                    3,
                    12,
                    0,
                    0,
                    TimeSpan.Zero)
        };

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
