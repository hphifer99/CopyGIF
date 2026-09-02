using System.Net;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;
using CopyGIF.Core.Settings;
using CopyGIF.Infrastructure.Media;
using CopyGIF.Infrastructure.Storage;
using CopyGIF.Infrastructure.Tests.TestDoubles;

namespace CopyGIF.Infrastructure.Tests.Media;

[TestClass]
public sealed class SecureGifDownloaderAdversarialTests
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
    public async Task DownloadAsync_UndeclaredOversizedStream_IsRejectedAndRemoved()
    {
        StreamContent content =
            new(
                new OversizedGifStream());

        Assert.IsNull(
            content.Headers.ContentLength,
            "The adversarial body must not advertise its size.");

        TestHttpMessageHandler handler =
            new(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content = content
                    });

        ApplicationPaths paths =
            new(
                Path.Combine(
                    _testDirectory,
                    "Profile"));

        FakeHostAddressResolver resolver =
            new();

        resolver.Add(
            "static.klipy.com",
            IPAddress.Parse(
                "93.184.216.34"));

        using HttpClient client =
            new(
                handler);

        SecureGifDownloader downloader =
            new(
                client,
                new MediaHostPolicy(
                    resolver,
                    [
                        "static.klipy.com"
                    ]),
                paths,
                new FakeSettingsStore(),
                new FakeClock(),
                new OwnedPathGuard());

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => downloader.DownloadAsync(
                    CreateItem(),
                    GifDownloadPurpose.Clipboard));

        Assert.AreEqual(
            MediaDownloadFailure.TooLarge,
            exception.Failure);

        Assert.IsEmpty(
            Directory.GetFiles(
                paths.ClipboardCacheDirectory));
    }

    private static GifItem CreateItem()
    {
        return new GifItem
        {
            ProviderId = "klipy",
            Id = "oversized-stream",
            Title = "Oversized",
            ThumbnailUri =
                new Uri(
                    "https://static.klipy.com/thumb.jpg"),
            PreviewUri =
                new Uri(
                    "https://static.klipy.com/preview.gif"),
            GifUri =
                new Uri(
                    "https://static.klipy.com/original.gif")
        };
    }

    private sealed class OversizedGifStream :
        Stream
    {
        private static readonly byte[] Header =
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
            0
        ];

        private readonly long _length =
            MediaPolicy.MaximumGifBytes +
            1;

        private long _position;

        public override bool CanRead =>
            true;

        public override bool CanSeek =>
            false;

        public override bool CanWrite =>
            false;

        public override long Length =>
            throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(
            byte[] buffer,
            int offset,
            int count)
        {
            return ReadCore(
                buffer.AsSpan(
                    offset,
                    count));
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                ReadCore(
                    buffer.Span));
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

        private int ReadCore(
            Span<byte> destination)
        {
            long remaining =
                _length -
                _position;

            if (remaining <= 0)
            {
                return 0;
            }

            int count =
                (int)Math.Min(
                    destination.Length,
                    remaining);

            destination[..count]
                .Clear();

            if (_position <
                Header.Length)
            {
                int headerOffset =
                    (int)_position;

                int headerCount =
                    Math.Min(
                        count,
                        Header.Length -
                        headerOffset);

                Header.AsSpan(
                        headerOffset,
                        headerCount)
                    .CopyTo(
                        destination);
            }

            _position += count;

            return count;
        }
    }

    private sealed class FakeSettingsStore :
        ISettingsStore
    {
        public Task<AppSettings> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                new AppSettings());
        }

        public Task SaveAsync(
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock :
        IClock
    {
        public DateTimeOffset UtcNow
        {
            get;
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

            return Task.CompletedTask;
        }
    }
}
