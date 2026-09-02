using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;
using CopyGIF.Core.Settings;
using CopyGIF.Infrastructure.Media;
using CopyGIF.Infrastructure.Storage;
using CopyGIF.Infrastructure.Tests.TestDoubles;

namespace CopyGIF.Infrastructure.Tests.Media;

[TestClass]
public sealed class SecureGifDownloaderTests
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
    public async Task DownloadAsync_ValidGif_StoresAndHashesFile()
    {
        byte[] gif =
            CreateValidGif();

        TestHttpMessageHandler handler =
            new(
                _ => GifResponse(
                    gif));

        using TestContext context =
            CreateContext(
                handler);

        DownloadedGif result =
            await context.Downloader
                .DownloadAsync(
                    CreateItem(),
                    GifDownloadPurpose.Clipboard);

        Assert.IsTrue(
            File.Exists(
                result.FilePath));

        CollectionAssert.AreEqual(
            gif,
            await File.ReadAllBytesAsync(
                result.FilePath));

        Assert.AreEqual(
            gif.LongLength,
            result.SizeBytes);

        Assert.AreEqual(
            Convert.ToHexString(
                    SHA256.HashData(
                        gif))
                .ToLowerInvariant(),
            result.Sha256);

        Assert.AreEqual(
            context.Clock.UtcNow,
            result.DownloadedAtUtc);

        Assert.AreEqual(
            GifDownloadPurpose.Clipboard,
            result.Purpose);

        StringAssert.StartsWith(
            result.FilePath,
            context.Paths
                .ClipboardCacheDirectory);

        Assert.AreEqual(
            0,
            context.SettingsStore.LoadCount);
    }

    [TestMethod]
    public async Task DownloadAsync_Favorite_UsesCustomLibraryRoot()
    {
        string customRoot =
            Path.Combine(
                _testDirectory,
                "SelectedLibrary");

        TestHttpMessageHandler handler =
            new(
                _ => GifResponse(
                    CreateValidGif()));

        using TestContext context =
            CreateContext(
                handler,
                customRoot);

        DownloadedGif result =
            await context.Downloader
                .DownloadAsync(
                    CreateItem(),
                    GifDownloadPurpose.Favorite);

        StringAssert.StartsWith(
            result.FilePath,
            context.Paths
                .GetFavoritesDirectory(
                    customRoot));

        Assert.AreEqual(
            1,
            context.SettingsStore.LoadCount);
    }

    [TestMethod]
    public async Task DownloadAsync_ApprovedRedirect_StoresFinalResponse()
    {
        int requestCount = 0;

        TestHttpMessageHandler handler =
            new(
                _ =>
                {
                    requestCount++;

                    if (requestCount == 1)
                    {
                        return RedirectResponse(
                            "/final.gif");
                    }

                    return GifResponse(
                        CreateValidGif());
                });

        using TestContext context =
            CreateContext(
                handler);

        DownloadedGif result =
            await context.Downloader
                .DownloadAsync(
                    CreateItem(),
                    GifDownloadPurpose.Clipboard);

        Assert.AreEqual(
            2,
            requestCount);

        Assert.AreEqual(
            new Uri(
                "https://static.klipy.com/final.gif"),
            result.SourceUri);
    }

    [TestMethod]
    public async Task DownloadAsync_RedirectToUnapprovedHost_IsRejected()
    {
        int requestCount = 0;

        TestHttpMessageHandler handler =
            new(
                _ =>
                {
                    requestCount++;

                    return RedirectResponse(
                        "https://example.com/unsafe.gif");
                });

        using TestContext context =
            CreateContext(
                handler);

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => context.Downloader
                    .DownloadAsync(
                        CreateItem(),
                        GifDownloadPurpose.Clipboard));

        Assert.AreEqual(
            MediaDownloadFailure.UnapprovedHost,
            exception.Failure);

        Assert.AreEqual(
            1,
            requestCount);
    }

    [TestMethod]
    public async Task DownloadAsync_TooManyRedirects_IsRejected()
    {
        int requestCount = 0;

        TestHttpMessageHandler handler =
            new(
                _ =>
                {
                    requestCount++;

                    return RedirectResponse(
                        $"/redirect-{requestCount}.gif");
                });

        using TestContext context =
            CreateContext(
                handler);

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => context.Downloader
                    .DownloadAsync(
                        CreateItem(),
                        GifDownloadPurpose.Clipboard));

        Assert.AreEqual(
            MediaDownloadFailure.RedirectLimitExceeded,
            exception.Failure);

        Assert.AreEqual(
            MediaPolicy.MaximumRedirects + 1,
            requestCount);
    }

    [TestMethod]
    public async Task DownloadAsync_DeclaredOversizedBody_IsRejected()
    {
        ByteArrayContent content =
            new(
                CreateValidGif());

        content.Headers.ContentLength =
            MediaPolicy.MaximumGifBytes + 1;

        TestHttpMessageHandler handler =
            new(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content = content
                    });

        using TestContext context =
            CreateContext(
                handler);

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => context.Downloader
                    .DownloadAsync(
                        CreateItem(),
                        GifDownloadPurpose.Clipboard));

        Assert.AreEqual(
            MediaDownloadFailure.TooLarge,
            exception.Failure);

        Assert.IsEmpty(
            Directory.GetFiles(
                context.Paths
                    .ClipboardCacheDirectory));
    }

    [TestMethod]
    public async Task DownloadAsync_InvalidGif_IsRejectedAndTemporaryFileRemoved()
    {
        TestHttpMessageHandler handler =
            new(
                _ => GifResponse(
                    "not a gif"u8.ToArray()));

        using TestContext context =
            CreateContext(
                handler);

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => context.Downloader
                    .DownloadAsync(
                        CreateItem(),
                        GifDownloadPurpose.Clipboard));

        Assert.AreEqual(
            MediaDownloadFailure.InvalidGif,
            exception.Failure);

        Assert.IsEmpty(
            Directory.GetFiles(
                context.Paths
                    .ClipboardCacheDirectory));
    }

    [TestMethod]
    public async Task DownloadAsync_Timeout_IsClassified()
    {
        TestHttpMessageHandler handler =
            new(
                _ => throw new TaskCanceledException(
                    "Simulated timeout."));

        using TestContext context =
            CreateContext(
                handler);

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => context.Downloader
                    .DownloadAsync(
                        CreateItem(),
                        GifDownloadPurpose.Clipboard));

        Assert.AreEqual(
            MediaDownloadFailure.Timeout,
            exception.Failure);
    }

    [TestMethod]
    public async Task DownloadAsync_HttpFailure_PreservesStatusCode()
    {
        TestHttpMessageHandler handler =
            new(
                _ => new HttpResponseMessage(
                    HttpStatusCode.NotFound));

        using TestContext context =
            CreateContext(
                handler);

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => context.Downloader
                    .DownloadAsync(
                        CreateItem(),
                        GifDownloadPurpose.Clipboard));

        Assert.AreEqual(
            MediaDownloadFailure.HttpError,
            exception.Failure);

        Assert.AreEqual(
            404,
            exception.HttpStatusCode);
    }

    private TestContext CreateContext(
        HttpMessageHandler handler,
        string? customLibraryRoot = null)
    {
        ApplicationPaths paths =
            new(
                Path.Combine(
                    _testDirectory,
                    "Profile"));

        FakeSettingsStore settingsStore =
            new(
                new AppSettings
                {
                    Library =
                        new LibrarySettings
                        {
                            CustomStorageRoot =
                                customLibraryRoot
                        }
                });

        FakeHostAddressResolver resolver =
            new();

        resolver.Add(
            "static.klipy.com",
            IPAddress.Parse(
                "93.184.216.34"));

        MediaHostPolicy policy =
            new(
                resolver,
                [
                    "static.klipy.com"
                ]);

        FakeClock clock =
            new();

        HttpClient client =
            new(handler);

        SecureGifDownloader downloader =
            new(
                client,
                policy,
                paths,
                settingsStore,
                clock,
                new OwnedPathGuard());

        return new TestContext(
            downloader,
            client,
            paths,
            settingsStore,
            clock);
    }

    private static GifItem CreateItem()
    {
        return new GifItem
        {
            ProviderId = "klipy",
            Id = "hello-hi-662",
            Title = "Hello",
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

    private static HttpResponseMessage GifResponse(
        byte[] content)
    {
        ByteArrayContent body =
            new(content);

        body.Headers.ContentType =
            new MediaTypeHeaderValue(
                "image/gif");

        return new HttpResponseMessage(
            HttpStatusCode.OK)
        {
            Content = body
        };
    }

    private static HttpResponseMessage RedirectResponse(
        string location)
    {
        HttpResponseMessage response =
            new(
                HttpStatusCode.Redirect);

        response.Headers.Location =
            new Uri(
                location,
                UriKind.RelativeOrAbsolute);

        return response;
    }

    private sealed record TestContext(
        SecureGifDownloader Downloader,
        HttpClient Client,
        ApplicationPaths Paths,
        FakeSettingsStore SettingsStore,
        FakeClock Clock) : IDisposable
    {
        public void Dispose()
        {
            Client.Dispose();
        }
    }

    private sealed class FakeSettingsStore :
        ISettingsStore
    {
        private AppSettings _settings;

        public FakeSettingsStore(
            AppSettings settings)
        {
            _settings = settings;
        }

        public int LoadCount
        {
            get;
            private set;
        }

        public Task<AppSettings> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LoadCount++;

            return Task.FromResult(
                _settings);
        }

        public Task SaveAsync(
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            _settings = settings;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock :
        IClock
    {
        public DateTimeOffset UtcNow
        {
            get;
            init;
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
