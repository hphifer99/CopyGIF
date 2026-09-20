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
        GifItem item = CreateItem();
        PreviewCoordinator coordinator = CreateCoordinator(CacheFor(item.ThumbnailUri, PreviewCacheKind.Thumbnail));
        Uri result = await coordinator.GetThumbnailSourceAsync(item);
        Assert.IsTrue(result.IsFile);
        Assert.AreEqual(Path.GetFullPath("validated.cache"), result.LocalPath);
    }

    [TestMethod]
    public async Task GetThumbnailSourceAsync_WithCacheMiss_DoesNotReturnRemoteUri()
    {
        PreviewCoordinator coordinator = CreateCoordinator(new FakePreviewCache());
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            coordinator.GetThumbnailSourceAsync(CreateItem()));
    }

    [TestMethod]
    public async Task GetAnimatedSourceAsync_WhenAnimationsDisabled_UsesCachedThumbnail()
    {
        GifItem item = CreateItem();
        PreviewCoordinator coordinator = CreateCoordinator(
            CacheFor(item.ThumbnailUri, PreviewCacheKind.Thumbnail), animatePreviews: false);
        Assert.IsTrue((await coordinator.GetAnimatedSourceAsync(item, false)).IsFile);
    }

    [TestMethod]
    public async Task GetAnimatedSourceAsync_WithReducedMotion_UsesCachedThumbnail()
    {
        GifItem item = CreateItem();
        PreviewCoordinator coordinator = CreateCoordinator(
            CacheFor(item.ThumbnailUri, PreviewCacheKind.Thumbnail));
        Assert.IsTrue((await coordinator.GetAnimatedSourceAsync(item, true)).IsFile);
    }

    [TestMethod]
    public async Task GetAnimatedSourceAsync_WithAnimationEnabled_UsesCachedPreview()
    {
        GifItem item = CreateItem();
        PreviewCoordinator coordinator = CreateCoordinator(
            CacheFor(item.PreviewUri!, PreviewCacheKind.Preview));
        Assert.IsTrue((await coordinator.GetAnimatedSourceAsync(item, false)).IsFile);
    }

    [TestMethod]
    public async Task GetAnimatedSourceAsync_WithoutPreviewUri_UsesCachedGif()
    {
        GifItem item = CreateItem() with { PreviewUri = null };
        PreviewCoordinator coordinator = CreateCoordinator(
            CacheFor(item.GifUri, PreviewCacheKind.Preview));
        Assert.IsTrue((await coordinator.GetAnimatedSourceAsync(item, false)).IsFile);
    }

    [TestMethod]
    public async Task InvalidateAsync_RemovesThumbnailAndPreviewEntries()
    {
        GifItem item = CreateItem();
        List<(Uri, PreviewCacheKind)> removed = [];
        FakePreviewCache cache = new()
        {
            RemoveHandler = (uri, kind, _) =>
            {
                removed.Add((uri, kind));
                return Task.CompletedTask;
            }
        };
        await CreateCoordinator(cache).InvalidateAsync(item);
        CollectionAssert.AreEqual(
            new[] { (item.ThumbnailUri, PreviewCacheKind.Thumbnail), (item.PreviewUri!, PreviewCacheKind.Preview) },
            removed);
    }

    [TestMethod]
    public async Task CleanupAsync_UsesPreviewCache()
    {
        FakePreviewCache cache = new();
        await CreateCoordinator(cache).CleanupAsync();
        Assert.AreEqual(1, cache.CleanupCallCount);
    }

    private static FakePreviewCache CacheFor(Uri expectedUri, PreviewCacheKind expectedKind) => new()
    {
        TryGetHandler = (uri, kind, _) =>
        {
            Assert.AreEqual(expectedUri, uri);
            Assert.AreEqual(expectedKind, kind);
            return Task.FromResult<PreviewCacheEntry?>(new PreviewCacheEntry
            {
                SourceUri = uri,
                Kind = kind,
                FilePath = Path.GetFullPath("validated.cache"),
                SizeBytes = 128,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastAccessedAtUtc = DateTimeOffset.UtcNow
            });
        }
    };

    private static PreviewCoordinator CreateCoordinator(FakePreviewCache cache, bool animatePreviews = true) =>
        new(new FakeSettingsStore
        {
            Value = new AppSettings { Search = new SearchSettings { AnimatePreviews = animatePreviews } }
        }, cache);

    private static GifItem CreateItem() => new()
    {
        ProviderId = "klipy",
        Id = "cat-1",
        ThumbnailUri = new Uri("https://static.klipy.com/cat-thumb.gif"),
        PreviewUri = new Uri("https://static.klipy.com/cat-preview.gif"),
        GifUri = new Uri("https://static.klipy.com/cat.gif")
    };
}
