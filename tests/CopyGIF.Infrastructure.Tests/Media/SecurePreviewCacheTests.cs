using System.Net;
using CopyGIF.Core.Models;
using CopyGIF.Infrastructure.Media;
using CopyGIF.Infrastructure.Storage;
using CopyGIF.Infrastructure.Tests.TestDoubles;
using CopyGIF.Infrastructure.Time;

namespace CopyGIF.Infrastructure.Tests.Media;

[TestClass]
public sealed class SecurePreviewCacheTests
{
    [TestMethod]
    public async Task CacheMiss_DownloadsValidatedFile_AndNextRequestUsesCache()
    {
        int requests = 0;
        using Harness harness = new(_ =>
        {
            requests++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ValidGif())
            };
        });

        Uri source = new("https://static.klipy.com/preview.gif");
        PreviewCacheEntry? first = await harness.Cache.TryGetAsync(source, PreviewCacheKind.Preview);
        PreviewCacheEntry? second = await harness.Cache.TryGetAsync(source, PreviewCacheKind.Preview);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.AreEqual(1, requests);
        Assert.AreEqual(first.FilePath, second.FilePath);
        CollectionAssert.AreEqual(ValidGif(), await File.ReadAllBytesAsync(first.FilePath));
    }

    [TestMethod]
    public async Task RedirectToUnapprovedHost_IsRejectedBeforeSecondRequest()
    {
        int requests = 0;
        using Harness harness = new(_ =>
        {
            requests++;
            HttpResponseMessage response = new(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://localhost/private.gif");
            return response;
        });

        await Assert.ThrowsExactlyAsync<MediaDownloadException>(() =>
            harness.Cache.TryGetAsync(new Uri("https://static.klipy.com/redirect"), PreviewCacheKind.Preview));
        Assert.AreEqual(1, requests);
    }

    [TestMethod]
    public async Task PrivateResolvedAddress_IsRejectedBeforeRequest()
    {
        int requests = 0;
        using Harness harness = new(_ =>
        {
            requests++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, IPAddress.Loopback);

        await Assert.ThrowsExactlyAsync<MediaDownloadException>(() =>
            harness.Cache.TryGetAsync(new Uri("https://static.klipy.com/preview.gif"), PreviewCacheKind.Preview));
        Assert.AreEqual(0, requests);
    }

    [TestMethod]
    public async Task InvalidMedia_IsNotPublishedToCache()
    {
        using Harness harness = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>This is not a GIF.</html>")
        });

        await Assert.ThrowsExactlyAsync<MediaDownloadException>(() =>
            harness.Cache.TryGetAsync(new Uri("https://static.klipy.com/preview.gif"), PreviewCacheKind.Preview));
        Assert.AreEqual(0, Directory.GetFiles(harness.Root, "*.cache", SearchOption.AllDirectories).Length);
    }

    [TestMethod]
    public async Task StreamExceedsDeclaredSize_StillEnforcesThumbnailLimit()
    {
        byte[] oversized = new byte[5 * 1024 * 1024 + 1];
        ValidGif().CopyTo(oversized, 0);
        using Harness harness = new(_ =>
        {
            ByteArrayContent content = new(oversized);
            content.Headers.ContentLength = 1;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        await Assert.ThrowsExactlyAsync<MediaDownloadException>(() =>
            harness.Cache.TryGetAsync(new Uri("https://static.klipy.com/thumb.gif"), PreviewCacheKind.Thumbnail));
        Assert.AreEqual(0, Directory.GetFiles(harness.Root, "*.cache", SearchOption.AllDirectories).Length);
    }

    private static byte[] ValidGif() =>
        Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    private sealed class Harness : IDisposable
    {
        private readonly PreviewCache _inner;
        private readonly TestHttpMessageHandler _handler;
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "CopyGIF.Tests", Guid.NewGuid().ToString("N"));
        public SecurePreviewCache Cache { get; }

        public Harness(Func<HttpRequestMessage, HttpResponseMessage> response, IPAddress? address = null)
        {
            ApplicationPaths paths = new(Root);
            paths.EnsureDirectoriesExist();
            PreviewCacheLimits limits = PreviewCacheLimits.Default;
            _inner = new PreviewCache(paths, new SystemClock(), new OwnedPathGuard(), limits);
            _handler = new TestHttpMessageHandler(response);
            Cache = new SecurePreviewCache(_inner, new ClientFactory(_handler),
                new MediaHostPolicy(new FixedResolver(address ?? IPAddress.Parse("8.8.8.8")),
                    ["static.klipy.com"]), limits);
        }

        public void Dispose()
        {
            Cache.Dispose();
            _inner.Dispose();
            _handler.Dispose();
            Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FixedResolver(IPAddress address) : IHostAddressResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken = default) =>
            Task.FromResult(new[] { address });
    }
}
