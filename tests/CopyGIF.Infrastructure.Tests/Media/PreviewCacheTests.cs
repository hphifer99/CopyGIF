using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Infrastructure.Media;
using CopyGIF.Infrastructure.Storage;

namespace CopyGIF.Infrastructure.Tests.Media;

[TestClass]
public sealed class PreviewCacheTests
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
        if (Directory.Exists(
                _testDirectory))
        {
            Directory.Delete(
                _testDirectory,
                recursive: true);
        }
    }

    [TestMethod]
    public async Task StoreAndTryGet_Thumbnail_RoundTripsAndTouchesEntry()
    {
        TestContext context =
            CreateContext();

        Uri uri =
            CreateUri(
                "thumbnail.jpg");

        byte[] jpeg =
            CreateJpeg(
                12);

        await using MemoryStream content =
            new(
                jpeg);

        PreviewCacheEntry stored =
            await context.Cache
                .StoreAsync(
                    uri,
                    PreviewCacheKind.Thumbnail,
                    content);

        context.Clock.UtcNow =
            context.Clock.UtcNow
                .AddHours(1);

        PreviewCacheEntry? found =
            await context.Cache
                .TryGetAsync(
                    uri,
                    PreviewCacheKind.Thumbnail);

        Assert.IsNotNull(
            found);

        Assert.AreEqual(
            stored.FilePath,
            found.FilePath);

        Assert.AreEqual(
            jpeg.LongLength,
            found.SizeBytes);

        Assert.AreEqual(
            context.Clock.UtcNow,
            found.LastAccessedAtUtc);

        CollectionAssert.AreEqual(
            jpeg,
            await File.ReadAllBytesAsync(
                found.FilePath));
    }

    [TestMethod]
    public async Task StoreAsync_ValidAnimatedPreview_StoresGif()
    {
        TestContext context =
            CreateContext();

        byte[] gif =
            CreateValidGif();

        await using MemoryStream content =
            new(
                gif);

        PreviewCacheEntry stored =
            await context.Cache
                .StoreAsync(
                    CreateUri(
                        "preview.gif"),
                    PreviewCacheKind.Preview,
                    content);

        Assert.AreEqual(
            PreviewCacheKind.Preview,
            stored.Kind);

        Assert.AreEqual(
            gif.LongLength,
            stored.SizeBytes);

        Assert.IsTrue(
            File.Exists(
                stored.FilePath));
    }

    [TestMethod]
    public async Task StoreAsync_NonGifPreview_IsRejectedAndTemporaryFileRemoved()
    {
        TestContext context =
            CreateContext();

        await using MemoryStream content =
            new(
                CreateJpeg(
                    12));

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => context.Cache
                    .StoreAsync(
                        CreateUri(
                            "not-animated.jpg"),
                        PreviewCacheKind.Preview,
                        content));

        Assert.AreEqual(
            MediaDownloadFailure.InvalidGif,
            exception.Failure);

        Assert.IsEmpty(
            Directory.GetFiles(
                context.Paths
                    .PreviewCacheDirectory));
    }

    [TestMethod]
    public async Task StoreAsync_StreamExceedsItemLimit_IsRejectedAndRemoved()
    {
        TestContext context =
            CreateContext(
                new PreviewCacheLimits
                {
                    MaximumThumbnailBytes = 8,
                    MaximumPreviewBytes = 32,
                    MaximumThumbnailCacheBytes = 32,
                    MaximumPreviewCacheBytes = 64,
                    Retention = TimeSpan.FromDays(1)
                });

        await using MemoryStream content =
            new(
                CreateJpeg(
                    9));

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => context.Cache
                    .StoreAsync(
                        CreateUri(
                            "too-large.jpg"),
                        PreviewCacheKind.Thumbnail,
                        content));

        Assert.AreEqual(
            MediaDownloadFailure.TooLarge,
            exception.Failure);

        Assert.IsEmpty(
            Directory.GetFiles(
                context.Paths
                    .ThumbnailCacheDirectory));
    }

    [TestMethod]
    public async Task RemoveAsync_ExistingEntry_RemovesOnlyRequestedKind()
    {
        TestContext context =
            CreateContext();

        Uri uri =
            CreateUri(
                "shared.gif");

        await StoreAsync(
            context.Cache,
            uri,
            PreviewCacheKind.Thumbnail,
            CreateValidGif());

        await StoreAsync(
            context.Cache,
            uri,
            PreviewCacheKind.Preview,
            CreateValidGif());

        await context.Cache
            .RemoveAsync(
                uri,
                PreviewCacheKind.Preview);

        Assert.IsNotNull(
            await context.Cache
                .TryGetAsync(
                    uri,
                    PreviewCacheKind.Thumbnail));

        Assert.IsNull(
            await context.Cache
                .TryGetAsync(
                    uri,
                    PreviewCacheKind.Preview));
    }

    [TestMethod]
    public async Task CleanupAsync_ExpiredEntry_IsRemoved()
    {
        TestContext context =
            CreateContext();

        Uri uri =
            CreateUri(
                "expired.jpg");

        PreviewCacheEntry stored =
            await StoreAsync(
                context.Cache,
                uri,
                PreviewCacheKind.Thumbnail,
                CreateJpeg(
                    12));

        context.Clock.UtcNow =
            context.Clock.UtcNow
                .AddDays(2);

        File.SetLastWriteTimeUtc(
            stored.FilePath,
            context.Clock.UtcNow
                .AddDays(-2)
                .UtcDateTime);

        await context.Cache
            .CleanupAsync();

        Assert.IsFalse(
            File.Exists(
                stored.FilePath));
    }

    [TestMethod]
    public async Task StoreAsync_CacheExceedsLimit_EvictsLeastRecentlyUsedEntry()
    {
        TestContext context =
            CreateContext(
                new PreviewCacheLimits
                {
                    MaximumThumbnailBytes = 12,
                    MaximumPreviewBytes = 32,
                    MaximumThumbnailCacheBytes = 16,
                    MaximumPreviewCacheBytes = 64,
                    Retention = TimeSpan.FromDays(1)
                });

        Uri firstUri =
            CreateUri(
                "first.jpg");

        Uri secondUri =
            CreateUri(
                "second.jpg");

        await StoreAsync(
            context.Cache,
            firstUri,
            PreviewCacheKind.Thumbnail,
            CreateJpeg(
                10));

        context.Clock.UtcNow =
            context.Clock.UtcNow
                .AddMinutes(1);

        await StoreAsync(
            context.Cache,
            secondUri,
            PreviewCacheKind.Thumbnail,
            CreateJpeg(
                10));

        Assert.IsNull(
            await context.Cache
                .TryGetAsync(
                    firstUri,
                    PreviewCacheKind.Thumbnail));

        Assert.IsNotNull(
            await context.Cache
                .TryGetAsync(
                    secondUri,
                    PreviewCacheKind.Thumbnail));
    }

    [TestMethod]
    public async Task TryGetAsync_CorruptedEntry_RemovesIt()
    {
        TestContext context =
            CreateContext();

        Uri uri =
            CreateUri(
                "corrupt.gif");

        PreviewCacheEntry stored =
            await StoreAsync(
                context.Cache,
                uri,
                PreviewCacheKind.Preview,
                CreateValidGif());

        await File.WriteAllBytesAsync(
            stored.FilePath,
            "corrupt"u8.ToArray());

        PreviewCacheEntry? found =
            await context.Cache
                .TryGetAsync(
                    uri,
                    PreviewCacheKind.Preview);

        Assert.IsNull(
            found);

        Assert.IsFalse(
            File.Exists(
                stored.FilePath));
    }

    [TestMethod]
    public async Task CleanupAsync_OrphanedTemporaryFile_RemovesIt()
    {
        TestContext context =
            CreateContext();

        context.Paths
            .EnsureDirectoriesExist();

        string temporaryPath =
            Path.Combine(
                context.Paths
                    .ThumbnailCacheDirectory,
                ".orphan.tmp");

        await File.WriteAllTextAsync(
            temporaryPath,
            "partial");

        await context.Cache
            .CleanupAsync();

        Assert.IsFalse(
            File.Exists(
                temporaryPath));
    }

    [TestMethod]
    public async Task StoreAsync_NonHttpsSource_IsRejectedBeforeWriting()
    {
        TestContext context =
            CreateContext();

        await using MemoryStream content =
            new(
                CreateJpeg(
                    12));

        await Assert.ThrowsAsync<ArgumentException>(
            () => context.Cache
                .StoreAsync(
                    new Uri(
                        "http://static.klipy.com/thumb.jpg"),
                    PreviewCacheKind.Thumbnail,
                    content));

        Assert.IsFalse(
            Directory.Exists(
                context.Paths
                    .ThumbnailCacheDirectory));
    }

    [TestMethod]
    public async Task StoreAsync_SlowDownload_DoesNotBlockCacheHitsOrCleanup()
    {
        TestContext context =
            CreateContext();

        Uri fastUri =
            CreateUri(
                "fast.jpg");

        await StoreAsync(
            context.Cache,
            fastUri,
            PreviewCacheKind.Thumbnail,
            CreateJpeg(
                16));

        Uri slowUri =
            CreateUri(
                "slow.jpg");

        using BlockingStream slowContent =
            new(
                CreateJpeg(
                    16));

        Task<PreviewCacheEntry> slowStore =
            context.Cache
                .StoreAsync(
                    slowUri,
                    PreviewCacheKind.Thumbnail,
                    slowContent);

        await slowContent.ReadBlocked
            .WaitAsync(
                TimeSpan.FromSeconds(10));

        // A cache hit for a different item must not wait for the stalled download.
        Task<PreviewCacheEntry?> hit =
            context.Cache
                .TryGetAsync(
                    fastUri,
                    PreviewCacheKind.Thumbnail);

        Assert.AreSame(
            hit,
            await Task.WhenAny(
                hit,
                Task.Delay(
                    TimeSpan.FromSeconds(10))));

        Assert.IsNotNull(
            await hit);

        // Cleanup must leave the download that is still streaming alone.
        await context.Cache
            .CleanupAsync();

        Assert.HasCount(
            1,
            Directory.GetFiles(
                context.Paths
                    .ThumbnailCacheDirectory,
                "*.tmp"));

        slowContent.Release();

        PreviewCacheEntry stored =
            await slowStore;

        Assert.IsTrue(
            File.Exists(
                stored.FilePath));

        Assert.HasCount(
            0,
            Directory.GetFiles(
                context.Paths
                    .ThumbnailCacheDirectory,
                "*.tmp"));
    }

    private TestContext CreateContext(
        PreviewCacheLimits? limits = null)
    {
        ApplicationPaths paths =
            new(
                Path.Combine(
                    _testDirectory,
                    "Profile"));

        FakeClock clock =
            new();

        PreviewCache cache =
            new(
                paths,
                clock,
                new OwnedPathGuard(),
                limits ??
                new PreviewCacheLimits
                {
                    MaximumThumbnailBytes = 32,
                    MaximumPreviewBytes = 32,
                    MaximumThumbnailCacheBytes = 64,
                    MaximumPreviewCacheBytes = 64,
                    Retention = TimeSpan.FromDays(1)
                });

        return new TestContext(
            cache,
            paths,
            clock);
    }

    private static async Task<PreviewCacheEntry> StoreAsync(
        PreviewCache cache,
        Uri uri,
        PreviewCacheKind kind,
        byte[] bytes)
    {
        await using MemoryStream content =
            new(
                bytes);

        return await cache.StoreAsync(
            uri,
            kind,
            content);
    }

    private static Uri CreateUri(
        string fileName)
    {
        return new Uri(
            $"https://static.klipy.com/{fileName}");
    }

    private static byte[] CreateJpeg(
        int length)
    {
        byte[] bytes =
            new byte[length];

        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;

        return bytes;
    }

    private static byte[] CreateValidGif()
    {
        return
        [
            (byte)'G',
            (byte)'I',
            (byte)'F',
            (byte)'8',
            (byte)'9',
            (byte)'a',
            1,
            0,
            1,
            0,
            0,
            0,
            0,
            0x3B
        ];
    }

    private sealed class BlockingStream :
        Stream
    {
        private readonly byte[] _bytes;

        private readonly TaskCompletionSource _release =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        private readonly TaskCompletionSource _readBlocked =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        private int _position;

        private bool _headerDelivered;

        public BlockingStream(
            byte[] bytes)
        {
            _bytes = bytes;
        }

        public Task ReadBlocked =>
            _readBlocked.Task;

        public void Release()
        {
            _release.TrySetResult();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            // Deliver the whole valid header first, then stall until released.
            if (!_headerDelivered)
            {
                _headerDelivered = true;

                int count =
                    Math.Min(
                        buffer.Length,
                        _bytes.Length);

                _bytes.AsMemory(
                        0,
                        count)
                    .CopyTo(
                        buffer);

                _position = count;

                return count;
            }

            _readBlocked.TrySetResult();

            await _release.Task
                .WaitAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            return 0;
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override long Seek(
            long offset,
            SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(
            long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record TestContext(
        PreviewCache Cache,
        ApplicationPaths Paths,
        FakeClock Clock);

    private sealed class FakeClock :
        IClock
    {
        public DateTimeOffset UtcNow
        {
            get;
            set;
        } = new(
            2026,
            9,
            1,
            12,
            0,
            0,
            TimeSpan.Zero);

        public Task DelayAsync(
            TimeSpan delay,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            UtcNow += delay;

            return Task.CompletedTask;
        }
    }
}
