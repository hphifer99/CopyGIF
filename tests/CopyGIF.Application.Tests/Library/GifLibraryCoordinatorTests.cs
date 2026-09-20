using CopyGIF.Application.Library;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Library;

[TestClass]
public sealed class GifLibraryCoordinatorTests
{
    private static readonly DateTimeOffset ReferenceTime =
        new(
            2026,
            9,
            3,
            12,
            0,
            0,
            TimeSpan.Zero);

    [TestMethod]
    public async Task LoadAsync_ReturnsStoredSnapshot()
    {
        LibrarySnapshot expected =
            new()
            {
                Favorites =
                [
                    CreateEntry(
                        "favorite-1",
                        ReferenceTime)
                ]
            };

        FakeLibraryStore libraryStore =
            new()
            {
                Value = expected
            };

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore: libraryStore);

        LibrarySnapshot result =
            await coordinator.LoadAsync();

        Assert.AreSame(
            expected,
            result);
    }

    [TestMethod]
    public async Task AddFavoriteAsync_WithLocalStorage_DownloadsAndSaves()
    {
        FakeLibraryStore libraryStore =
            new();

        FakeGifDownloader downloader =
            new();

        FakeClock clock =
            new(
                ReferenceTime);

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore: libraryStore,
                downloader: downloader,
                clock: clock);

        GifItem item =
            CreateItem(
                "favorite-1");

        LibrarySnapshot result =
            await coordinator.AddFavoriteAsync(
                item);

        Assert.HasCount(
            1,
            downloader.Requests);

        Assert.AreEqual(
            GifDownloadPurpose.Favorite,
            downloader.Requests[0].Purpose);

        Assert.HasCount(
            1,
            libraryStore.SavedSnapshots);

        LibraryEntry favorite =
            result.Favorites[0];

        Assert.AreEqual(
            item.StableIdentity,
            favorite.Identity);

        Assert.AreEqual(
            ReferenceTime,
            favorite.AddedAtUtc);

        Assert.IsNotNull(
            favorite.LocalFilePath);

        Assert.AreEqual(
            0,
            favorite.CopyCount);
    }

    [TestMethod]
    public async Task AddFavoriteAsync_WhenDuplicate_DoesNotDownloadOrSave()
    {
        GifItem item =
            CreateItem(
                "favorite-1");

        LibrarySnapshot current =
            new()
            {
                Favorites =
                [
                    CreateEntry(
                        item.Id,
                        ReferenceTime)
                ]
            };

        FakeLibraryStore libraryStore =
            new()
            {
                Value = current
            };

        FakeGifDownloader downloader =
            new();

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore: libraryStore,
                downloader: downloader);

        LibrarySnapshot result =
            await coordinator.AddFavoriteAsync(
                item);

        Assert.AreSame(
            current,
            result);

        Assert.IsEmpty(
            downloader.Requests);

        Assert.IsEmpty(
            libraryStore.SavedSnapshots);
    }

    [TestMethod]
    public async Task AddFavoriteAsync_WhenLimitReached_PreservesExistingFavorites()
    {
        FakeLibraryStore libraryStore =
            new()
            {
                Value =
                    new LibrarySnapshot
                    {
                        Favorites =
                        [
                            CreateEntry(
                                "favorite-1",
                                ReferenceTime)
                        ]
                    }
            };

        FakeSettingsStore settingsStore =
            CreateSettingsStore(
                favoriteLimit: 1);

        FakeGifDownloader downloader =
            new();

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore,
                settingsStore,
                downloader);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () =>
            {
                await coordinator.AddFavoriteAsync(
                    CreateItem(
                        "favorite-2"));
            });

        Assert.IsEmpty(
            downloader.Requests);

        Assert.IsEmpty(
            libraryStore.SavedSnapshots);

        Assert.HasCount(
            1,
            libraryStore.Value.Favorites);
    }

    [TestMethod]
    public async Task AddFavoriteAsync_WithoutLocalStorage_SavesMetadataOnly()
    {
        FakeLibraryStore libraryStore =
            new();

        FakeGifDownloader downloader =
            new();

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore,
                CreateSettingsStore(
                    storeFavoritesLocally: false),
                downloader);

        LibrarySnapshot result =
            await coordinator.AddFavoriteAsync(
                CreateItem(
                    "favorite-1"));

        Assert.IsEmpty(
            downloader.Requests);

        Assert.IsNull(
            result.Favorites[0]
                .LocalFilePath);
    }

    [TestMethod]
    public async Task AddFavoriteAsync_WhenSaveFails_DeletesDownloadedFile()
    {
        FakeLibraryStore libraryStore =
            new()
            {
                SaveHandler =
                    (_, _) =>
                        Task.FromException(
                            new IOException(
                                "Save failed."))
            };

        FakeLibraryStorageMover storageMover =
            new();

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore: libraryStore,
                storageMover: storageMover);

        await Assert.ThrowsExactlyAsync<IOException>(
            async () =>
            {
                await coordinator.AddFavoriteAsync(
                    CreateItem(
                        "favorite-1"));
            });

        Assert.HasCount(
            1,
            storageMover.DeleteRequests);

        Assert.HasCount(
            1,
            storageMover.DeleteRequests[0]
                .FilePaths);

        Assert.IsEmpty(
            libraryStore.SavedSnapshots);
    }

    [TestMethod]
    public async Task RemoveFavoriteAsync_SavesBeforeDeletingLocalFile()
    {
        List<string> operations = [];

        FakeApplicationPaths paths =
            new();

        string localPath =
            Path.Combine(
                paths.FavoritesDirectory,
                "favorite-1.gif");

        FakeLibraryStore libraryStore =
            new()
            {
                Value =
                    new LibrarySnapshot
                    {
                        Favorites =
                        [
                            CreateEntry(
                                "favorite-1",
                                ReferenceTime,
                                localPath)
                        ]
                    },

                SaveHandler =
                    (_, _) =>
                    {
                        operations.Add(
                            "save");

                        return Task.CompletedTask;
                    }
            };

        FakeLibraryStorageMover storageMover =
            new()
            {
                DeleteHandler =
                    (_, _, _) =>
                    {
                        operations.Add(
                            "delete");

                        return Task.CompletedTask;
                    }
            };

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore: libraryStore,
                storageMover: storageMover,
                paths: paths);

        LibrarySnapshot result =
            await coordinator.RemoveFavoriteAsync(
                new GifIdentity(
                    "klipy",
                    "favorite-1"));

        Assert.IsEmpty(
            result.Favorites);

        CollectionAssert.AreEqual(
            new[]
            {
                "save",
                "delete"
            },
            operations);

        Assert.AreEqual(
            paths.RootDirectory,
            storageMover.DeleteRequests[0]
                .OwnedRoot);
    }

    [TestMethod]
    public async Task ClearFavoritesAsync_PreservesRecents()
    {
        LibraryEntry favorite =
            CreateEntry(
                "favorite-1",
                ReferenceTime);

        LibraryEntry recent =
            CreateEntry(
                "recent-1",
                ReferenceTime);

        FakeLibraryStore libraryStore =
            new()
            {
                Value =
                    new LibrarySnapshot
                    {
                        Favorites = [favorite],
                        Recents = [recent]
                    }
            };

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore: libraryStore);

        LibrarySnapshot result =
            await coordinator.ClearFavoritesAsync();

        Assert.IsEmpty(
            result.Favorites);

        Assert.HasCount(
            1,
            result.Recents);

        Assert.AreSame(
            recent,
            result.Recents[0]);
    }

    [TestMethod]
    public async Task RecordRecentAsync_MergesDuplicateAndIncrementsCopyCount()
    {
        GifItem item =
            CreateItem(
                "recent-1");

        DateTimeOffset firstCopied =
            ReferenceTime.AddDays(-1);

        LibraryEntry existing =
            CreateEntry(
                item.Id,
                firstCopied,
                localFilePath:
                    "C:\\Library\\old.gif",
                lastCopiedAtUtc:
                    firstCopied,
                copyCount: 2);

        FakeLibraryStore libraryStore =
            new()
            {
                Value =
                    new LibrarySnapshot
                    {
                        Recents = [existing]
                    }
            };

        DownloadedGif copiedGif =
            CreateDownloadedGif(
                item,
                GifDownloadPurpose.Recent,
                "C:\\Library\\new.gif");

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore: libraryStore,
                clock:
                    new FakeClock(
                        ReferenceTime));

        LibrarySnapshot result =
            await coordinator.RecordRecentAsync(
                item,
                copiedGif);

        Assert.HasCount(
            1,
            result.Recents);

        LibraryEntry updated =
            result.Recents[0];

        Assert.AreEqual(
            3,
            updated.CopyCount);

        Assert.AreEqual(
            firstCopied,
            updated.AddedAtUtc);

        Assert.AreEqual(
            ReferenceTime,
            updated.LastCopiedAtUtc);

        Assert.AreEqual(
            copiedGif.FilePath,
            updated.LocalFilePath);
    }

    [TestMethod]
    public async Task RecordRecentAsync_WithoutLocalStorage_SavesMetadataOnly()
    {
        GifItem item =
            CreateItem(
                "recent-1");

        FakeLibraryStorageMover storageMover =
            new();

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                settingsStore:
                    CreateSettingsStore(
                        storeRecentsLocally: false),
                storageMover: storageMover,
                clock:
                    new FakeClock(
                        ReferenceTime));

        LibrarySnapshot result =
            await coordinator.RecordRecentAsync(
                item,
                CreateDownloadedGif(
                    item,
                    GifDownloadPurpose.Clipboard,
                    "C:\\Cache\\recent-1.gif"));

        Assert.IsNull(
            result.Recents[0]
                .LocalFilePath);

        Assert.IsEmpty(
            storageMover.DeleteRequests);
    }

    [TestMethod]
    public async Task GiphyFavoritesAndRecents_PreserveProviderIdentity()
    {
        GifItem item = CreateItem("giphy-1") with { ProviderId = "giphy" };
        using GifLibraryCoordinator coordinator = CreateCoordinator(settingsStore:
            CreateSettingsStore(storeFavoritesLocally: false, storeRecentsLocally: false));

        await coordinator.AddFavoriteAsync(item);
        LibrarySnapshot result = await coordinator.RecordRecentAsync(item,
            CreateDownloadedGif(item, GifDownloadPurpose.Clipboard, "C:\\Cache\\giphy-1.gif"));

        Assert.AreEqual("giphy", result.Favorites.Single().Identity.ProviderId);
        Assert.AreEqual("giphy", result.Recents.Single().Identity.ProviderId);
    }

    [TestMethod]
    public async Task RecordRecentAsync_WhenLimitExceeded_EvictsOldestRecent()
    {
        FakeApplicationPaths paths =
            new();

        LibraryEntry first =
            CreateEntry(
                "recent-1",
                ReferenceTime.AddMinutes(-1),
                Path.Combine(
                    paths.RecentsDirectory,
                    "recent-1.gif"));

        LibraryEntry oldest =
            CreateEntry(
                "recent-2",
                ReferenceTime.AddMinutes(-2),
                Path.Combine(
                    paths.RecentsDirectory,
                    "recent-2.gif"));

        FakeLibraryStore libraryStore =
            new()
            {
                Value =
                    new LibrarySnapshot
                    {
                        Recents =
                        [
                            first,
                            oldest
                        ]
                    }
            };

        FakeLibraryStorageMover storageMover =
            new();

        GifItem newItem =
            CreateItem(
                "recent-3");

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore,
                CreateSettingsStore(
                    recentLimit: 2),
                storageMover: storageMover,
                paths: paths,
                clock:
                    new FakeClock(
                        ReferenceTime));

        LibrarySnapshot result =
            await coordinator.RecordRecentAsync(
                newItem,
                CreateDownloadedGif(
                    newItem,
                    GifDownloadPurpose.Recent,
                    Path.Combine(
                        paths.RecentsDirectory,
                        "recent-3.gif")));

        CollectionAssert.AreEqual(
            new[]
            {
                "recent-3",
                "recent-1"
            },
            result.Recents
                .Select(
                    entry => entry.Identity.Id)
                .ToArray());

        Assert.HasCount(
            1,
            storageMover.DeleteRequests);

        CollectionAssert.AreEqual(
            new[]
            {
                oldest.LocalFilePath!
            },
            storageMover.DeleteRequests[0]
                .FilePaths
                .ToArray());
    }

    [TestMethod]
    public async Task RecordRecentAsync_WithMismatchedIdentity_DoesNotLoadOrSave()
    {
        GifItem item =
            CreateItem(
                "recent-1");

        GifItem differentItem =
            CreateItem(
                "recent-2");

        FakeLibraryStore libraryStore =
            new();

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore: libraryStore);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            async () =>
            {
                await coordinator.RecordRecentAsync(
                    item,
                    CreateDownloadedGif(
                        differentItem,
                        GifDownloadPurpose.Recent,
                        "C:\\Library\\recent-2.gif"));
            });

        Assert.AreEqual(
            0,
            libraryStore.LoadCallCount);

        Assert.IsEmpty(
            libraryStore.SavedSnapshots);
    }

    [TestMethod]
    public async Task ClearRecentsAsync_PreservesFavoritesAndDeletesRecentFiles()
    {
        FakeApplicationPaths paths =
            new();

        LibraryEntry favorite =
            CreateEntry(
                "favorite-1",
                ReferenceTime);

        LibraryEntry recent =
            CreateEntry(
                "recent-1",
                ReferenceTime,
                Path.Combine(
                    paths.RecentsDirectory,
                    "recent-1.gif"));

        FakeLibraryStore libraryStore =
            new()
            {
                Value =
                    new LibrarySnapshot
                    {
                        Favorites = [favorite],
                        Recents = [recent]
                    }
            };

        FakeLibraryStorageMover storageMover =
            new();

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore: libraryStore,
                storageMover: storageMover,
                paths: paths);

        LibrarySnapshot result =
            await coordinator.ClearRecentsAsync();

        Assert.HasCount(
            1,
            result.Favorites);

        Assert.IsEmpty(
            result.Recents);

        Assert.HasCount(
            1,
            storageMover.DeleteRequests);
    }

    [TestMethod]
    public async Task AddFavoriteAsync_SerializesConcurrentMutations()
    {
        TaskCompletionSource<bool> firstSaveStarted =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        TaskCompletionSource<bool> releaseFirstSave =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        int saveNumber = 0;

        FakeLibraryStore libraryStore =
            new()
            {
                SaveHandler =
                    async (_, cancellationToken) =>
                    {
                        int currentSave =
                            Interlocked.Increment(
                                ref saveNumber);

                        if (currentSave == 1)
                        {
                            firstSaveStarted.TrySetResult(
                                true);

                            await releaseFirstSave.Task
                                .WaitAsync(
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
            };

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore,
                CreateSettingsStore(
                    storeFavoritesLocally: false));

        Task<LibrarySnapshot> firstMutation =
            coordinator.AddFavoriteAsync(
                CreateItem(
                    "favorite-1"));

        await firstSaveStarted.Task;

        Task<LibrarySnapshot> secondMutation =
            coordinator.AddFavoriteAsync(
                CreateItem(
                    "favorite-2"));

        Assert.AreEqual(
            1,
            libraryStore.LoadCallCount);

        releaseFirstSave.TrySetResult(
            true);

        await Task.WhenAll(
            firstMutation,
            secondMutation);

        Assert.HasCount(
            2,
            libraryStore.Value.Favorites);

        CollectionAssert.AreEquivalent(
            new[]
            {
                "favorite-1",
                "favorite-2"
            },
            libraryStore.Value.Favorites
                .Select(
                    entry => entry.Identity.Id)
                .ToArray());
    }

    [TestMethod]
    public async Task FavoritesAndRecents_UseSaveQualityInsteadOfCopyQuality()
    {
        var settings = new FakeSettingsStore { Value = new AppSettings
        {
            Library = new LibrarySettings { GifQuality = GifQuality.High, SaveQuality = GifQuality.Low }
        }};
        var downloader = new FakeGifDownloader();
        var item = CreateItem("separate-qualities") with
        {
            Renditions = new GifRenditions
            {
                Low = new Uri("https://static.klipy.com/low.gif"),
                High = new Uri("https://static.klipy.com/high.gif")
            }
        };
        using var coordinator = CreateCoordinator(settingsStore: settings, downloader: downloader);
        await coordinator.AddFavoriteAsync(item);
        await coordinator.RecordRecentAsync(item, CreateDownloadedGif(item,
            GifDownloadPurpose.Clipboard, Path.Combine(Path.GetTempPath(), "clipboard.gif")));
        Assert.HasCount(2, downloader.Requests);
        Assert.IsTrue(downloader.Requests.All(r => r.Item.GifUri == item.Renditions.Low));
        Assert.AreEqual(GifDownloadPurpose.Favorite, downloader.Requests[0].Purpose);
        Assert.AreEqual(GifDownloadPurpose.Recent, downloader.Requests[1].Purpose);
    }

    private static GifItem CreateItemWithSeparateQualities(
        string id) =>
        CreateItem(id) with
        {
            Renditions = new GifRenditions
            {
                Low = new Uri("https://static.klipy.com/low.gif"),
                High = new Uri("https://static.klipy.com/high.gif")
            }
        };

    private static FakeSettingsStore CreateSeparateQualitySettings() =>
        new()
        {
            Value = new AppSettings
            {
                Library = new LibrarySettings
                {
                    GifQuality = GifQuality.High,
                    SaveQuality = GifQuality.Low
                }
            }
        };

    [TestMethod]
    public async Task RecordRecentAsync_SecondDownload_DoesNotHoldTheLibraryLock()
    {
        FakeSettingsStore settings = CreateSeparateQualitySettings();
        FakeGifDownloader downloader = new();
        TaskCompletionSource<DownloadedGif> slowDownload =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        GifItem item = CreateItemWithSeparateQualities("slow-save-quality");

        downloader.DownloadHandler =
            (_, purpose, _) =>
                purpose == GifDownloadPurpose.Recent
                    ? slowDownload.Task
                    : throw new InvalidOperationException("Only the Recent download is expected.");

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                settingsStore: settings,
                downloader: downloader);

        Task<LibrarySnapshot> record =
            coordinator.RecordRecentAsync(
                item,
                CreateDownloadedGif(
                    item,
                    GifDownloadPurpose.Clipboard,
                    Path.Combine(
                        Path.GetTempPath(),
                        "clipboard.gif")));

        while (downloader.Requests.Count == 0)
        {
            await Task.Delay(10);
        }

        // The slow network download is in flight. Other library work must not queue behind it.
        Task<LibrarySnapshot> otherWork =
            coordinator.LoadAsync();

        Task finished =
            await Task.WhenAny(
                otherWork,
                Task.Delay(
                    TimeSpan.FromSeconds(3)));

        Assert.AreSame(
            otherWork,
            finished,
            "reading the library must not wait for a Recent's second download");

        Assert.IsFalse(
            record.IsCompleted);

        slowDownload.SetResult(
            CreateDownloadedGif(
                item,
                GifDownloadPurpose.Recent,
                Path.Combine(
                    Path.GetTempPath(),
                    "recent-low.gif")));

        LibrarySnapshot snapshot =
            await record;

        Assert.HasCount(
            1,
            snapshot.Recents);
    }

    [TestMethod]
    public async Task RecordRecentAsync_SaveFailsAfterTheSecondDownload_RemovesTheDownloadedFile()
    {
        FakeSettingsStore settings = CreateSeparateQualitySettings();
        FakeLibraryStore libraryStore = new();
        FakeLibraryStorageMover mover = new();
        GifItem item = CreateItemWithSeparateQualities("save-fails");
        string downloadedPath =
            Path.Combine(
                Path.GetTempPath(),
                "recent-low.gif");

        FakeGifDownloader downloader = new()
        {
            DownloadHandler =
                (_, _, _) =>
                    Task.FromResult(
                        CreateDownloadedGif(
                            item,
                            GifDownloadPurpose.Recent,
                            downloadedPath))
        };

        libraryStore.SaveHandler =
            static (_, _) =>
                throw new IOException("The disk is full.");

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore: libraryStore,
                settingsStore: settings,
                downloader: downloader,
                storageMover: mover);

        await Assert.ThrowsExactlyAsync<IOException>(
            () => coordinator.RecordRecentAsync(
                item,
                CreateDownloadedGif(
                    item,
                    GifDownloadPurpose.Clipboard,
                    Path.Combine(
                        Path.GetTempPath(),
                        "clipboard.gif"))));

        Assert.IsTrue(
            mover.DeleteRequests.Any(
                request =>
                    request.FilePaths.Contains(
                        downloadedPath)),
            "the file downloaded for a Recent that was never saved must not be left behind");
    }

    [TestMethod]
    public async Task RecordRecentAsync_SaveFails_KeepsAFileThatAnExistingEntryAlreadyUses()
    {
        FakeSettingsStore settings = CreateSeparateQualitySettings();
        FakeLibraryStorageMover mover = new();
        GifItem item = CreateItemWithSeparateQualities("shared-path");
        string sharedPath =
            Path.Combine(
                Path.GetTempPath(),
                "recent-low.gif");

        FakeLibraryStore libraryStore = new()
        {
            Value =
                new LibrarySnapshot
                {
                    Recents =
                    [
                        CreateEntry(
                            "shared-path",
                            ReferenceTime,
                            localFilePath: sharedPath)
                    ]
                }
        };

        FakeGifDownloader downloader = new()
        {
            DownloadHandler =
                (_, _, _) =>
                    Task.FromResult(
                        CreateDownloadedGif(
                            item,
                            GifDownloadPurpose.Recent,
                            sharedPath))
        };

        libraryStore.SaveHandler =
            static (_, _) =>
                throw new IOException("The disk is full.");

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore: libraryStore,
                settingsStore: settings,
                downloader: downloader,
                storageMover: mover);

        await Assert.ThrowsExactlyAsync<IOException>(
            () => coordinator.RecordRecentAsync(
                item,
                CreateDownloadedGif(
                    item,
                    GifDownloadPurpose.Clipboard,
                    Path.Combine(
                        Path.GetTempPath(),
                        "clipboard.gif"))));

        Assert.IsFalse(
            mover.DeleteRequests.Any(
                request =>
                    request.FilePaths.Contains(
                        sharedPath)),
            "the older Recent still points at this file");
    }

    [TestMethod]
    public async Task RecordRecentAsync_CancelledWhileWaitingForTheLock_RemovesTheSecondDownload()
    {
        FakeSettingsStore settings = CreateSeparateQualitySettings();
        FakeLibraryStore libraryStore = new();
        FakeLibraryStorageMover mover = new();
        GifItem item = CreateItemWithSeparateQualities("cancelled");
        string downloadedPath =
            Path.Combine(
                Path.GetTempPath(),
                "recent-low.gif");

        FakeGifDownloader downloader = new()
        {
            DownloadHandler =
                (_, _, _) =>
                    Task.FromResult(
                        CreateDownloadedGif(
                            item,
                            GifDownloadPurpose.Recent,
                            downloadedPath))
        };

        // Another operation holds the library lock (its first read never finishes until released).
        TaskCompletionSource<LibrarySnapshot> holdLock =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        int loads = 0;

        libraryStore.LoadHandler =
            _ =>
                Interlocked.Increment(ref loads) == 1
                    ? holdLock.Task
                    : Task.FromResult(new LibrarySnapshot());

        using GifLibraryCoordinator coordinator =
            CreateCoordinator(
                libraryStore: libraryStore,
                settingsStore: settings,
                downloader: downloader,
                storageMover: mover);

        Task<LibrarySnapshot> holder =
            coordinator.LoadAsync();

        while (Volatile.Read(ref loads) == 0)
        {
            await Task.Delay(10);
        }

        using CancellationTokenSource cancellation = new();

        Task<LibrarySnapshot> record =
            coordinator.RecordRecentAsync(
                item,
                CreateDownloadedGif(
                    item,
                    GifDownloadPurpose.Clipboard,
                    Path.Combine(
                        Path.GetTempPath(),
                        "clipboard.gif")),
                cancellation.Token);

        while (downloader.Requests.Count == 0)
        {
            await Task.Delay(10);
        }

        await Task.Delay(50);

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => record);

        holdLock.SetResult(
            new LibrarySnapshot());

        await holder;

        Assert.IsTrue(
            mover.DeleteRequests.Any(
                request =>
                    request.FilePaths.Contains(
                        downloadedPath)));
    }

    private static GifLibraryCoordinator CreateCoordinator(
        FakeLibraryStore? libraryStore = null,
        FakeSettingsStore? settingsStore = null,
        FakeGifDownloader? downloader = null,
        FakeLibraryStorageMover? storageMover = null,
        FakeApplicationPaths? paths = null,
        FakeClock? clock = null)
    {
        return new GifLibraryCoordinator(
            libraryStore ??
                new FakeLibraryStore(),
            settingsStore ??
                CreateSettingsStore(),
            downloader ??
                new FakeGifDownloader(),
            storageMover ??
                new FakeLibraryStorageMover(),
            paths ??
                new FakeApplicationPaths(),
            clock ??
                new FakeClock(
                    ReferenceTime));
    }

    private static FakeSettingsStore CreateSettingsStore(
        int recentLimit = 30,
        int favoriteLimit = 100,
        bool storeFavoritesLocally = true,
        bool storeRecentsLocally = true)
    {
        return new FakeSettingsStore
        {
            Value =
                new AppSettings
                {
                    Library =
                        new LibrarySettings
                        {
                            RecentLimit =
                                recentLimit,
                            FavoriteLimit =
                                favoriteLimit,
                            StoreFavoritesLocally =
                                storeFavoritesLocally,
                            StoreRecentsLocally =
                                storeRecentsLocally
                        }
                }
        };
    }

    private static GifItem CreateItem(
        string id)
    {
        return new GifItem
        {
            ProviderId = "klipy",
            Id = id,
            Title = $"GIF {id}",
            Description = "Description",
            ThumbnailUri =
                new Uri(
                    $"https://static.klipy.com/{id}-thumb.gif"),
            PreviewUri =
                new Uri(
                    $"https://static.klipy.com/{id}-preview.gif"),
            GifUri =
                new Uri(
                    $"https://static.klipy.com/{id}.gif"),
            Width = 320,
            Height = 240,
            SizeBytes = 256
        };
    }

    private static LibraryEntry CreateEntry(
        string id,
        DateTimeOffset addedAtUtc,
        string? localFilePath = null,
        DateTimeOffset? lastCopiedAtUtc = null,
        int copyCount = 0)
    {
        return new LibraryEntry
        {
            Identity =
                new GifIdentity(
                    "klipy",
                    id),
            Title = $"GIF {id}",
            GifUri =
                new Uri(
                    $"https://static.klipy.com/{id}.gif"),
            ThumbnailUri =
                new Uri(
                    $"https://static.klipy.com/{id}-thumb.gif"),
            LocalFilePath = localFilePath,
            AddedAtUtc = addedAtUtc,
            LastCopiedAtUtc = lastCopiedAtUtc,
            CopyCount = copyCount
        };
    }

    private static DownloadedGif CreateDownloadedGif(
        GifItem item,
        GifDownloadPurpose purpose,
        string filePath)
    {
        return new DownloadedGif
        {
            Identity = item.StableIdentity,
            SourceUri = item.GifUri,
            FilePath = filePath,
            SizeBytes = item.SizeBytes ?? 256,
            Sha256 =
                new string(
                    '0',
                    64),
            DownloadedAtUtc = ReferenceTime,
            Purpose = purpose
        };
    }
}
