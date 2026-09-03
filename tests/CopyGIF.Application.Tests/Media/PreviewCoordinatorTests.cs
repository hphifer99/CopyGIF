using CopyGIF.Application.Media;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Media;

[TestClass]
public sealed class PreviewCoordinatorTests
{
    [TestMethod]
    public async Task GetThumbnailSourceAsync_WithCacheHit_ReturnsFileUri()
    {
        GifItem item =
            CreateItem();

        string cachedPath =
            Path.GetFullPath(
                Path.Combine(
                    "cache",
                    "cat-thumbnail.cache"));

        FakePreviewCache cache =
            new()
            {
                TryGetHandler =
                    (sourceUri, kind, _) =>
                    {
                        Assert.AreEqual(
                            item.ThumbnailUri,
                            sourceUri);

                        Assert.AreEqual(
                            PreviewCacheKind.Thumbnail,
                            kind);

                        return Task.FromResult<PreviewCacheEntry?>(
                            CreateCacheEntry(
                                sourceUri,
                                kind,
                                cachedPath));
                    }
            };

        PreviewCoordinator coordinator =
            CreateCoordinator(
                cache);

        Uri result =
            await coordinator.GetThumbnailSourceAsync(
                item);

        Assert.IsTrue(
            result.IsFile);

        Assert.AreEqual(
            Path.GetFullPath(
                cachedPath),
            Path.GetFullPath(
                result.LocalPath));
    }

    [TestMethod]
    public async Task GetThumbnailSourceAsync_WithCacheMiss_ReturnsRemoteUri()
    {
        GifItem item =
            CreateItem();

        PreviewCoordinator coordinator =
            CreateCoordinator(
                new FakePreviewCache());

        Uri result =
            await coordinator.GetThumbnailSourceAsync(
                item);

        Assert.AreEqual(
            item.ThumbnailUri,
            result);
    }

    [TestMethod]
    public async Task GetAnimatedSourceAsync_WhenAnimationsDisabled_UsesThumbnail()
    {
        GifItem item =
            CreateItem();

        List<PreviewCacheKind> requestedKinds = [];

        FakePreviewCache cache =
            new()
            {
                TryGetHandler =
                    (_, kind, _) =>
                    {
                        requestedKinds.Add(
                            kind);

                        return Task.FromResult<PreviewCacheEntry?>(
                            null);
                    }
            };

        PreviewCoordinator coordinator =
            CreateCoordinator(
                cache,
                animatePreviews: false);

        Uri result =
            await coordinator.GetAnimatedSourceAsync(
                item,
                reducedMotion: false);

        Assert.AreEqual(
            item.ThumbnailUri,
            result);

        CollectionAssert.AreEqual(
            new[]
            {
                PreviewCacheKind.Thumbnail
            },
            requestedKinds);
    }

    [TestMethod]
    public async Task GetAnimatedSourceAsync_WithReducedMotion_UsesThumbnail()
    {
        GifItem item =
            CreateItem();

        PreviewCoordinator coordinator =
            CreateCoordinator(
                new FakePreviewCache(),
                animatePreviews: true);

        Uri result =
            await coordinator.GetAnimatedSourceAsync(
                item,
                reducedMotion: true);

        Assert.AreEqual(
            item.ThumbnailUri,
            result);
    }

    [TestMethod]
    public async Task GetAnimatedSourceAsync_WithAnimationEnabled_UsesPreviewUri()
    {
        GifItem item =
            CreateItem();

        PreviewCoordinator coordinator =
            CreateCoordinator(
                new FakePreviewCache(),
                animatePreviews: true);

        Uri result =
            await coordinator.GetAnimatedSourceAsync(
                item,
                reducedMotion: false);

        Assert.AreEqual(
            item.PreviewUri,
            result);
    }

    [TestMethod]
    public async Task GetAnimatedSourceAsync_WithoutPreviewUri_UsesGifUri()
    {
        GifItem original =
            CreateItem();

        GifItem item =
            original with
            {
                PreviewUri = null
            };

        PreviewCoordinator coordinator =
            CreateCoordinator(
                new FakePreviewCache(),
                animatePreviews: true);

        Uri result =
            await coordinator.GetAnimatedSourceAsync(
                item,
                reducedMotion: false);

        Assert.AreEqual(
            item.GifUri,
            result);
    }

    [TestMethod]
    public async Task InvalidateAsync_RemovesThumbnailAndPreviewEntries()
    {
        GifItem item =
            CreateItem();

        List<(Uri SourceUri, PreviewCacheKind Kind)>
            removals = [];

        FakePreviewCache cache =
            new()
            {
                RemoveHandler =
                    (sourceUri, kind, _) =>
                    {
                        removals.Add(
                            (sourceUri, kind));

                        return Task.CompletedTask;
                    }
            };

        PreviewCoordinator coordinator =
            CreateCoordinator(
                cache);

        await coordinator.InvalidateAsync(
            item);

        Assert.HasCount(
            2,
            removals);

        Assert.AreEqual(
            (item.ThumbnailUri,
                PreviewCacheKind.Thumbnail),
            removals[0]);

        Assert.AreEqual(
            (item.PreviewUri!,
                PreviewCacheKind.Preview),
            removals[1]);
    }

    [TestMethod]
    public async Task CleanupAsync_UsesPreviewCache()
    {
        FakePreviewCache cache =
            new();

        PreviewCoordinator coordinator =
            CreateCoordinator(
                cache);

        await coordinator.CleanupAsync();

        Assert.AreEqual(
            1,
            cache.CleanupCallCount);
    }

    private static PreviewCoordinator CreateCoordinator(
        FakePreviewCache cache,
        bool animatePreviews = true)
    {
        FakeSettingsStore settingsStore =
            new()
            {
                Value =
                    new AppSettings
                    {
                        Search =
                            new SearchSettings
                            {
                                AnimatePreviews =
                                    animatePreviews
                            }
                    }
            };

        return new PreviewCoordinator(
            settingsStore,
            cache);
    }

    private static GifItem CreateItem()
    {
        return new GifItem
        {
            ProviderId = "klipy",
            Id = "cat-1",
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

    private static PreviewCacheEntry CreateCacheEntry(
        Uri sourceUri,
        PreviewCacheKind kind,
        string filePath)
    {
        DateTimeOffset timestamp =
            new(
                2026,
                9,
                3,
                12,
                0,
                0,
                TimeSpan.Zero);

        return new PreviewCacheEntry
        {
            SourceUri = sourceUri,
            Kind = kind,
            FilePath = filePath,
            SizeBytes = 128,
            CreatedAtUtc = timestamp,
            LastAccessedAtUtc = timestamp
        };
    }
}
