using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Infrastructure.Klipy;

public sealed class KlipyGifProvider : IGifProvider
{
    public const string ProviderId = "klipy";

    private const string MediaFilter =
        "gif,mediumgif,tinygif,tinygifpreview";

    private readonly HttpClient _httpClient;
    private readonly ISecretStore _secretStore;

    public KlipyGifProvider(
        HttpClient httpClient,
        ISecretStore secretStore)
    {
        _httpClient = httpClient ??
            throw new ArgumentNullException(
                nameof(httpClient));

        _secretStore = secretStore ??
            throw new ArgumentNullException(
                nameof(secretStore));
    }

    public string Id => ProviderId;

    public string DisplayName => "KLIPY";

    public async Task<GifSearchPage> SearchAsync(
        GifSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return new GifSearchPage
            {
                Items = [],
                ContinuationToken = null
            };
        }

        if (request.PageSize < 1 ||
            request.PageSize > 50)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Page size must be between 1 and 50.");
        }

        string apiKey =
            await GetApiKeyAsync(
                cancellationToken);

        string requestUri =
            BuildSearchUri(
                apiKey,
                request);

        KlipySearchResponseDto response =
            await SendAsync<KlipySearchResponseDto>(
                requestUri,
                cancellationToken);

        List<GifItem> items =
            new(response.Results.Count);

        foreach (KlipyResultDto result
                 in response.Results)
        {
            items.Add(MapResult(result));
        }

        return new GifSearchPage
        {
            Items = items,
            ContinuationToken =
                string.IsNullOrWhiteSpace(response.Next)
                    ? null
                    : response.Next
        };
    }

    public async Task<CredentialValidationResult>
        ValidateCredentialAsync(
            string credential,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credential))
        {
            return CredentialValidationResult.Invalid(
                "An API key is required.");
        }

        string requestUri =
            "v2/featured" +
            "?key=" +
            Escape(credential.Trim()) +
            "&limit=1" +
            "&media_filter=tinygif";

        try
        {
            await SendAsync<KlipySearchResponseDto>(
                requestUri,
                cancellationToken);

            return CredentialValidationResult.Valid();
        }
        catch (GifProviderException exception)
            when (
                exception.Failure ==
                    GifProviderFailure.Unauthorized)
        {
            return CredentialValidationResult.Invalid(
                "KLIPY did not accept that API key.");
        }
    }

    public async Task RegisterShareAsync(
        string itemId,
        string? searchQuery,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            itemId);

        string apiKey =
            await GetApiKeyAsync(
                cancellationToken);

        string requestUri =
            "v2/registershare" +
            "?key=" +
            Escape(apiKey) +
            "&id=" +
            Escape(itemId);

        if (searchQuery is not null)
        {
            requestUri +=
                "&q=" +
                Escape(searchQuery);
        }

        using HttpRequestMessage request =
            new(
                HttpMethod.Get,
                requestUri);

        using HttpResponseMessage response =
            await SendRequestAsync(
                request,
                cancellationToken);

        EnsureSuccess(response);
    }

    private async Task<string> GetApiKeyAsync(
        CancellationToken cancellationToken)
    {
        string? apiKey =
            await _secretStore.GetAsync(
                SecretNames.KlipyApiKey,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new GifProviderException(
                ProviderId,
                GifProviderFailure.MissingCredential,
                "A KLIPY API key has not been configured.");
        }

        return apiKey.Trim();
    }

    private async Task<T> SendAsync<T>(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request =
            new(
                HttpMethod.Get,
                requestUri);

        using HttpResponseMessage response =
            await SendRequestAsync(
                request,
                cancellationToken);

        EnsureSuccess(response);

        try
        {
            T? result =
                await response.Content
                    .ReadFromJsonAsync<T>(
                        cancellationToken:
                            cancellationToken);

            if (result is null)
            {
                throw new GifProviderException(
                    ProviderId,
                    GifProviderFailure.InvalidResponse,
                    "KLIPY returned an empty response.");
            }

            return result;
        }
        catch (JsonException exception)
        {
            throw new GifProviderException(
                ProviderId,
                GifProviderFailure.InvalidResponse,
                "KLIPY returned an invalid response.",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new GifProviderException(
                ProviderId,
                GifProviderFailure.InvalidResponse,
                "KLIPY returned an unsupported response.",
                exception);
        }
    }

    private async Task<HttpResponseMessage>
        SendRequestAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new GifProviderException(
                ProviderId,
                GifProviderFailure.Network,
                "The KLIPY request timed out.");
        }
        catch (HttpRequestException exception)
        {
            throw new GifProviderException(
                ProviderId,
                GifProviderFailure.Network,
                "KLIPY could not be reached.",
                exception);
        }
    }

    private static void EnsureSuccess(
        HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        GifProviderFailure failure =
            response.StatusCode switch
            {
                HttpStatusCode.Unauthorized =>
                    GifProviderFailure.Unauthorized,

                HttpStatusCode.Forbidden =>
                    GifProviderFailure.Unauthorized,

                HttpStatusCode.TooManyRequests =>
                    GifProviderFailure.RateLimited,

                HttpStatusCode.BadGateway =>
                    GifProviderFailure.ServiceUnavailable,

                HttpStatusCode.ServiceUnavailable =>
                    GifProviderFailure.ServiceUnavailable,

                HttpStatusCode.GatewayTimeout =>
                    GifProviderFailure.ServiceUnavailable,

                _ =>
                    GifProviderFailure.Unknown
            };

        string message =
            failure switch
            {
                GifProviderFailure.Unauthorized =>
                    "KLIPY rejected the API credential.",

                GifProviderFailure.RateLimited =>
                    "KLIPY temporarily rate limited the request.",

                GifProviderFailure.ServiceUnavailable =>
                    "KLIPY is temporarily unavailable.",

                _ =>
                    $"KLIPY returned HTTP {(int)response.StatusCode}."
            };

        throw new GifProviderException(
            ProviderId,
            failure,
            message);
    }

    private static GifItem MapResult(
        KlipyResultDto result)
    {
        if (string.IsNullOrWhiteSpace(result.Id))
        {
            throw InvalidResponse(
                "A KLIPY result did not contain an ID.");
        }

        KlipyMediaDto fullGif =
            GetRequiredMedia(
                result,
                "gif");

        KlipyMediaDto preview =
            GetMedia(result, "mediumgif") ??
            GetMedia(result, "tinygif") ??
            fullGif;

        KlipyMediaDto thumbnail =
            GetMedia(result, "tinygifpreview") ??
            GetMedia(result, "tinygif") ??
            preview;

        if (!Uri.TryCreate(
                fullGif.Url,
                UriKind.Absolute,
                out Uri? gifUri) ||
            gifUri.Scheme != Uri.UriSchemeHttps)
        {
            throw InvalidResponse(
                "KLIPY returned an invalid GIF URL.");
        }

        if (!Uri.TryCreate(
                preview.Url,
                UriKind.Absolute,
                out Uri? previewUri) ||
            previewUri.Scheme != Uri.UriSchemeHttps)
        {
            throw InvalidResponse(
                "KLIPY returned an invalid preview URL.");
        }

        if (!Uri.TryCreate(
                thumbnail.Url,
                UriKind.Absolute,
                out Uri? thumbnailUri) ||
            thumbnailUri.Scheme != Uri.UriSchemeHttps)
        {
            throw InvalidResponse(
                "KLIPY returned an invalid thumbnail URL.");
        }

        int width = 0;
        int height = 0;

        if (fullGif.Dimensions.Length >= 2)
        {
            width = fullGif.Dimensions[0];
            height = fullGif.Dimensions[1];
        }

        return new GifItem
        {
            ProviderId = ProviderId,
            Id = result.Id,
            Title = result.Title,
            Description =
                result.ContentDescription,
            ThumbnailUri = thumbnailUri,
            GifUri = gifUri,
            PreviewUri = previewUri,
            Width = width,
            Height = height
        };
    }

    private static KlipyMediaDto GetRequiredMedia(
        KlipyResultDto result,
        string format)
    {
        return GetMedia(result, format) ??
            throw InvalidResponse(
                $"KLIPY did not return the required {format} media.");
    }

    private static KlipyMediaDto? GetMedia(
        KlipyResultDto result,
        string format)
    {
        if (!result.MediaFormats.TryGetValue(
                format,
                out KlipyMediaDto? media))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(media.Url)
            ? null
            : media;
    }

    private static GifProviderException
        InvalidResponse(string message)
    {
        return new GifProviderException(
            ProviderId,
            GifProviderFailure.InvalidResponse,
            message);
    }

    private static string BuildSearchUri(
        string apiKey,
        GifSearchRequest request)
    {
        string uri =
            "v2/search" +
            "?key=" +
            Escape(apiKey) +
            "&q=" +
            Escape(request.Query) +
            "&limit=" +
            request.PageSize +
            "&media_filter=" +
            Escape(MediaFilter);

        if (!string.IsNullOrWhiteSpace(
                request.ContinuationToken))
        {
            uri +=
                "&pos=" +
                Escape(request.ContinuationToken);
        }

        return uri;
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value);
    }
}