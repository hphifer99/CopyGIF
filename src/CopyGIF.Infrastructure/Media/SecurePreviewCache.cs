using System.Net;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;

namespace CopyGIF.Infrastructure.Media;

public sealed class SecurePreviewCache : IPreviewCache, IDisposable
{
    private readonly PreviewCache _cache;
    private readonly IHttpClientFactory _clientFactory;
    private readonly MediaHostPolicy _hostPolicy;
    private readonly PreviewCacheLimits _limits;
    private readonly SemaphoreSlim _downloads =
        new(MediaPolicy.MaximumConcurrentMediaRequests);

    public SecurePreviewCache(
        PreviewCache cache,
        IHttpClientFactory clientFactory,
        MediaHostPolicy hostPolicy,
        PreviewCacheLimits limits)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _hostPolicy = hostPolicy ?? throw new ArgumentNullException(nameof(hostPolicy));
        _limits = limits ?? throw new ArgumentNullException(nameof(limits));
    }

    public async Task<PreviewCacheEntry?> TryGetAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        CancellationToken cancellationToken = default)
    {
        PreviewCacheEntry? cached = await _cache.TryGetAsync(sourceUri, kind, cancellationToken)
            .ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        using CancellationTokenSource deadline =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        bool entered = false;

        try
        {
            await _downloads.WaitAsync(deadline.Token).ConfigureAwait(false);
            entered = true;

            cached = await _cache.TryGetAsync(sourceUri, kind, deadline.Token)
                .ConfigureAwait(false);
            if (cached is not null)
            {
                return cached;
            }

            using HttpClient client = _clientFactory.CreateClient(nameof(SecurePreviewCache));
            Uri currentUri = sourceUri;
            long maximumBytes = kind == PreviewCacheKind.Thumbnail
                ? _limits.MaximumThumbnailBytes
                : _limits.MaximumPreviewBytes;

            for (int redirects = 0; redirects <= MediaPolicy.MaximumRedirects; redirects++)
            {
                await _hostPolicy.ValidateAsync(currentUri, deadline.Token).ConfigureAwait(false);
                using HttpRequestMessage request = new(HttpMethod.Get, currentUri);
                using HttpResponseMessage response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                    .ConfigureAwait(false);

                if (IsRedirect(response.StatusCode))
                {
                    if (redirects == MediaPolicy.MaximumRedirects)
                    {
                        throw new MediaDownloadException(MediaDownloadFailure.RedirectLimitExceeded,
                            "The preview exceeded the redirect limit.");
                    }

                    Uri? location = response.Headers.Location;
                    if (location is null || !Uri.TryCreate(currentUri, location, out Uri? nextUri))
                    {
                        throw new MediaDownloadException(MediaDownloadFailure.InvalidUri,
                            "The preview server returned an invalid redirect.");
                    }

                    currentUri = nextUri;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new MediaDownloadException(MediaDownloadFailure.HttpError,
                        $"The preview server returned HTTP {(int)response.StatusCode}.",
                        httpStatusCode: (int)response.StatusCode);
                }

                if (response.Content.Headers.ContentLength > maximumBytes)
                {
                    throw new MediaDownloadException(MediaDownloadFailure.TooLarge,
                        "The preview exceeds its download size limit.");
                }

                await using Stream content = await response.Content.ReadAsStreamAsync(deadline.Token)
                    .ConfigureAwait(false);

                // StoreAsync bounds the streamed bytes and validates the image signature
                // before it publishes a local cache file that the UI can decode.
                return await _cache.StoreAsync(sourceUri, kind, content, deadline.Token)
                    .ConfigureAwait(false);
            }

            throw new MediaDownloadException(MediaDownloadFailure.RedirectLimitExceeded,
                "The preview exceeded the redirect limit.");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MediaDownloadException(MediaDownloadFailure.Timeout,
                "The preview download timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new MediaDownloadException(MediaDownloadFailure.Network,
                "The preview could not be downloaded.", exception);
        }
        finally
        {
            if (entered)
            {
                _downloads.Release();
            }
        }
    }

    public Task<PreviewCacheEntry> StoreAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        Stream content,
        CancellationToken cancellationToken = default) =>
        _cache.StoreAsync(sourceUri, kind, content, cancellationToken);

    public Task RemoveAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        CancellationToken cancellationToken = default) =>
        _cache.RemoveAsync(sourceUri, kind, cancellationToken);

    public Task CleanupAsync(CancellationToken cancellationToken = default) =>
        _cache.CleanupAsync(cancellationToken);

    public void Dispose() => _downloads.Dispose();

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;
}
