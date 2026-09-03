using CopyGIF.Application.Library;
using CopyGIF.Application.Media;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Media;

[TestClass]
public sealed class GifCopyCoordinatorTests
{
    [TestMethod]
    public async Task CopyAsync_WithLocalRecents_DownloadsPersistentFile()
    {
        FakeGifProvider provider =
            new();

        FakeGifDownloader downloader =
            new();

        FakeClipboardService clipboard =
            new();

        FakeLibraryCoordinator library =
            new();

        GifCopyCoordinator coordinator =
            CreateCoordinator(
                provider,
                downloader,
                clipboard,
                library,
                storeRecentsLocally: true);

        GifItem item =
            CreateItem();

        DownloadedGif result =
            await coordinator.CopyAsync(
                item,
                "  funny cats  ");

        Assert.HasCount(
            1,
            downloader.Requests);

        Assert.AreEqual(
            GifDownloadPurpose.Recent,
            downloader.Requests[0].Purpose);

        Assert.HasCount(
            1,
            clipboard.CopiedGifs);

        Assert.HasCount(
            1,
            library.RecordedRecents);

        Assert.AreSame(
            result,
            library.RecordedRecents[0]
                .CopiedGif);

        Assert.HasCount(
            1,
            provider.ShareRegistrations);

        Assert.AreEqual(
            "funny cats",
            provider.ShareRegistrations[0]
                .SearchQuery);
    }

    [TestMethod]
    public async Task CopyAsync_WithoutLocalRecents_UsesClipboardCache()
    {
        FakeGifDownloader downloader =
            new();

        GifCopyCoordinator coordinator =
            CreateCoordinator(
                new FakeGifProvider(),
                downloader,
                new FakeClipboardService(),
                new FakeLibraryCoordinator(),
                storeRecentsLocally: false);

        await coordinator.CopyAsync(
            CreateItem(),
            searchQuery: null);

        Assert.AreEqual(
            GifDownloadPurpose.Clipboard,
            downloader.Requests[0].Purpose);
    }

    [TestMethod]
    public async Task CopyAsync_WhenDownloadFails_DoesNotTouchClipboardOrLibrary()
    {
        FakeGifDownloader downloader =
            new()
            {
                DownloadHandler =
                    (_, _, _) =>
                        Task.FromException<DownloadedGif>(
                            new IOException(
                                "Download failed."))
            };

        FakeClipboardService clipboard =
            new();

        FakeLibraryCoordinator library =
            new();

        FakeGifProvider provider =
            new();

        GifCopyCoordinator coordinator =
            CreateCoordinator(
                provider,
                downloader,
                clipboard,
                library);

        await Assert.ThrowsExactlyAsync<IOException>(
            async () =>
            {
                await coordinator.CopyAsync(
                    CreateItem(),
                    "cats");
            });

        Assert.IsEmpty(
            clipboard.CopyAttempts);

        Assert.IsEmpty(
            library.RecordedRecents);

        Assert.IsEmpty(
            provider.ShareRegistrations);
    }

    [TestMethod]
    public async Task CopyAsync_WhenClipboardFails_DoesNotCommitRecentOrShare()
    {
        FakeClipboardService clipboard =
            new()
            {
                CopyHandler =
                    (_, _) =>
                        Task.FromException(
                            new IOException(
                                "Clipboard failed."))
            };

        FakeLibraryCoordinator library =
            new();

        FakeGifProvider provider =
            new();

        GifCopyCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeGifDownloader(),
                clipboard,
                library);

        await Assert.ThrowsExactlyAsync<IOException>(
            async () =>
            {
                await coordinator.CopyAsync(
                    CreateItem(),
                    "cats");
            });

        Assert.HasCount(
            1,
            clipboard.CopyAttempts);

        Assert.IsEmpty(
            clipboard.CopiedGifs);

        Assert.IsEmpty(
            library.RecordedRecents);

        Assert.IsEmpty(
            provider.ShareRegistrations);
    }

    [TestMethod]
    public async Task CopyAsync_RecordsRecentBeforeRegisteringShare()
    {
        List<string> operations = [];

        FakeLibraryCoordinator library =
            new()
            {
                RecordRecentHandler =
                    (_, _, _) =>
                    {
                        operations.Add(
                            "recent");

                        return Task.CompletedTask;
                    }
            };

        FakeGifProvider provider =
            new()
            {
                ShareRegistrationHandler =
                    (_, _, _) =>
                    {
                        operations.Add(
                            "share");

                        return Task.CompletedTask;
                    }
            };

        GifCopyCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeGifDownloader(),
                new FakeClipboardService(),
                library);

        await coordinator.CopyAsync(
            CreateItem(),
            "cats");

        CollectionAssert.AreEqual(
            new[]
            {
                "recent",
                "share"
            },
            operations);
    }

    [TestMethod]
    public async Task CopyAsync_WhenShareRegistrationFails_PreservesCopySuccess()
    {
        FakeGifProvider provider =
            new()
            {
                ShareRegistrationHandler =
                    (_, _, _) =>
                        Task.FromException(
                            new GifProviderException(
                                "klipy",
                                GifProviderFailure.Network,
                                "Share registration failed."))
            };

        FakeClipboardService clipboard =
            new();

        FakeLibraryCoordinator library =
            new();

        GifCopyCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeGifDownloader(),
                clipboard,
                library);

        DownloadedGif result =
            await coordinator.CopyAsync(
                CreateItem(),
                "cats");

        Assert.IsNotNull(
            result);

        Assert.HasCount(
            1,
            clipboard.CopiedGifs);

        Assert.HasCount(
            1,
            library.RecordedRecents);
    }

    [TestMethod]
    public async Task CopyAsync_WhenClipboardIsCancelled_DoesNotCommitRecent()
    {
        using CancellationTokenSource cancellation =
            new();

        FakeClipboardService clipboard =
            new()
            {
                CopyHandler =
                    (_, cancellationToken) =>
                    {
                        cancellation.Cancel();

                        return Task.FromCanceled(
                            cancellationToken);
                    }
            };

        FakeLibraryCoordinator library =
            new();

        GifCopyCoordinator coordinator =
            CreateCoordinator(
                new FakeGifProvider(),
                new FakeGifDownloader(),
                clipboard,
                library);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
            {
                await coordinator.CopyAsync(
                    CreateItem(),
                    "cats",
                    cancellation.Token);
            });

        Assert.IsEmpty(
            library.RecordedRecents);
    }

    private static GifCopyCoordinator CreateCoordinator(
        FakeGifProvider provider,
        FakeGifDownloader downloader,
        FakeClipboardService clipboard,
        FakeLibraryCoordinator library,
        bool storeRecentsLocally = true)
    {
        FakeSettingsStore settingsStore =
            new()
            {
                Value =
                    new AppSettings
                    {
                        Library =
                            new LibrarySettings
                            {
                                StoreRecentsLocally =
                                    storeRecentsLocally
                            }
                    }
            };

        return new GifCopyCoordinator(
            settingsStore,
            downloader,
            clipboard,
            library,
            new FakeProviderCatalog(
                provider));
    }

    private static GifItem CreateItem()
    {
        return new GifItem
        {
            ProviderId = "klipy",
            Id = "cat-1",
            Title = "Cat",
            ThumbnailUri =
                new Uri(
                    "https://static.klipy.com/cat-thumb.gif"),
            PreviewUri =
                new Uri(
                    "https://static.klipy.com/cat-preview.gif"),
            GifUri =
                new Uri(
                    "https://static.klipy.com/cat.gif")
        };
    }

    private sealed record RecordedRecent(
        GifItem Item,
        DownloadedGif CopiedGif);

    private sealed class FakeLibraryCoordinator :
        IGifLibraryCoordinator
    {
        private readonly List<RecordedRecent>
            _recordedRecents = [];

        public Func<
            GifItem,
            DownloadedGif,
            CancellationToken,
            Task>? RecordRecentHandler
        { get; init; }

        public IReadOnlyList<RecordedRecent>
            RecordedRecents =>
                _recordedRecents.ToArray();

        public Task<LibrarySnapshot> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new LibrarySnapshot());
        }

        public Task<LibrarySnapshot> AddFavoriteAsync(
            GifItem item,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new LibrarySnapshot());
        }

        public Task<LibrarySnapshot> RemoveFavoriteAsync(
            GifIdentity identity,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new LibrarySnapshot());
        }

        public Task<LibrarySnapshot> ClearFavoritesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new LibrarySnapshot());
        }

        public async Task<LibrarySnapshot> RecordRecentAsync(
            GifItem item,
            DownloadedGif copiedGif,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (RecordRecentHandler is not null)
            {
                await RecordRecentHandler(
                        item,
                        copiedGif,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            _recordedRecents.Add(
                new RecordedRecent(
                    item,
                    copiedGif));

            return new LibrarySnapshot();
        }

        public Task<LibrarySnapshot> ClearRecentsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new LibrarySnapshot());
        }
    }

    private sealed class FakeProviderCatalog :
        IProviderCatalog
    {
        private readonly IGifProvider _provider;

        public FakeProviderCatalog(
            IGifProvider provider)
        {
            _provider = provider;
        }

        public IReadOnlyList<ProviderDescriptor>
            Providers => [];

        public IGifProvider GetRequiredProvider(
            string providerId)
        {
            if (!string.Equals(
                    providerId,
                    _provider.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new KeyNotFoundException();
            }

            return _provider;
        }

        public bool TryGetProvider(
            string providerId,
            out IGifProvider? provider)
        {
            bool found =
                string.Equals(
                    providerId,
                    _provider.Id,
                    StringComparison.OrdinalIgnoreCase);

            provider =
                found
                    ? _provider
                    : null;

            return found;
        }
    }
}
