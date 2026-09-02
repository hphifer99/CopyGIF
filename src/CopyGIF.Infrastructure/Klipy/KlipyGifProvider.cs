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

    private const int MaximumApiResponseBytes =
        2 * 1024 * 1024;

    private static readonly JsonSerializerOptions
        SerializerOptions =
            new(JsonSerializerDefaults.Web);

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
        ArgumentNullException.ThrowIfNull(
            request);

        if (request.Kind ==
                GifSearchKind.Search &&
            string.IsNullOrWhiteSpace(
                request.Query))
        {
            return GifSearchPage.Empty();
        }

        string apiKey =
            await GetApiKeyAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        string requestUri =
            KlipyRequestBuilder.BuildSearch(
                apiKey,
                request);

        KlipyResponseDto response =
            await SendForResponseAsync(
                    requestUri,
                    cancellationToken)
                .ConfigureAwait(false);

        return KlipyResponseMapper.Map(
            response);
    }

    public async Task<CredentialValidationResult>
        ValidateCredentialAsync(
            string credential,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                credential))
        {
            return CredentialValidationResult.Invalid(
                "An API key is required.",
                CredentialValidationFailure.MissingCredential);
        }

        string requestUri =
            KlipyRequestBuilder
                .BuildCredentialValidation(
                    credential.Trim());

        try
        {
            KlipyResponseDto response =
                await SendForResponseAsync(
                        requestUri,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!response.Result)
            {
                return CredentialValidationResult.Invalid(
                    "KLIPY did not accept that API key.");
            }

            return CredentialValidationResult.Valid();
        }
        catch (GifProviderException exception)
        {
            return ToCredentialValidationResult(
                exception);
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
                    cancellationToken)
                .ConfigureAwait(false);

        string requestUri =
            KlipyRequestBuilder.BuildShare(
                apiKey,
                itemId);

        Dictionary<string, string?> body =
            new(StringComparer.Ordinal)
            {
                ["q"] =
                    string.IsNullOrWhiteSpace(
                        searchQuery)
                        ? null
                        : searchQuery.Trim()
            };

        using HttpRequestMessage request =
            new(
                HttpMethod.Post,
                requestUri)
            {
                Content =
                    JsonContent.Create(
                        body)
            };

        using HttpResponseMessage response =
            await SendRequestAsync(
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

        EnsureSuccess(
            response);
    }

    private async Task<string> GetApiKeyAsync(
        CancellationToken cancellationToken)
    {
        string? apiKey =
            await _secretStore.GetAsync(
                    SecretNames.KlipyApiKey,
                    cancellationToken)
                .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(
                apiKey))
        {
            throw new GifProviderException(
                ProviderId,
                GifProviderFailure.MissingCredential,
                "A KLIPY API key has not been configured.");
        }

        return apiKey.Trim();
    }

    private async Task<KlipyResponseDto>
        SendForResponseAsync(
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
                    cancellationToken)
                .ConfigureAwait(false);

        EnsureSuccess(
            response);

        return await ReadResponseAsync(
                response,
                cancellationToken)
            .ConfigureAwait(false);
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
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new GifProviderException(
                ProviderId,
                GifProviderFailure.Timeout,
                "The KLIPY request timed out.",
                exception);
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

    private static async Task<KlipyResponseDto>
        ReadResponseAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
    {
        long? contentLength =
            response.Content.Headers
                .ContentLength;

        if (contentLength >
            MaximumApiResponseBytes)
        {
            throw InvalidResponse(
                "KLIPY returned an oversized response.");
        }

        await using Stream source =
            await response.Content
                .ReadAsStreamAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        using MemoryStream buffer =
            new();

        byte[] chunk =
            new byte[81920];

        while (true)
        {
            int read =
                await source.ReadAsync(
                        chunk,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read >
                MaximumApiResponseBytes)
            {
                throw InvalidResponse(
                    "KLIPY returned an oversized response.");
            }

            await buffer.WriteAsync(
                    chunk.AsMemory(
                        0,
                        read),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        buffer.Position = 0;

        try
        {
            KlipyResponseDto? result =
                await JsonSerializer
                    .DeserializeAsync<
                        KlipyResponseDto>(
                        buffer,
                        SerializerOptions,
                        cancellationToken)
                    .ConfigureAwait(false);

            return result ??
                throw InvalidResponse(
                    "KLIPY returned an empty response.");
        }
        catch (JsonException exception)
        {
            throw new GifProviderException(
                ProviderId,
                GifProviderFailure.InvalidResponse,
                "KLIPY returned malformed JSON.",
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

                HttpStatusCode.NotFound =>
                    GifProviderFailure.Unauthorized,

                HttpStatusCode.TooManyRequests =>
                    GifProviderFailure.RateLimited,

                HttpStatusCode.RequestTimeout =>
                    GifProviderFailure.Timeout,

                _ when
                    (int)response.StatusCode >= 500 =>
                    GifProviderFailure.ServiceUnavailable,

                _ =>
                    GifProviderFailure.Unknown
            };

        TimeSpan? retryAfter =
            response.Headers.RetryAfter?.Delta;

        string message =
            failure switch
            {
                GifProviderFailure.Unauthorized =>
                    "KLIPY rejected the API credential.",

                GifProviderFailure.RateLimited =>
                    "KLIPY temporarily rate limited the request.",

                GifProviderFailure.Timeout =>
                    "The KLIPY request timed out.",

                GifProviderFailure.ServiceUnavailable =>
                    "KLIPY is temporarily unavailable.",

                _ =>
                    $"KLIPY returned HTTP {(int)response.StatusCode}."
            };

        throw new GifProviderException(
            ProviderId,
            failure,
            message,
            retryAfter: retryAfter);
    }

    private static CredentialValidationResult
        ToCredentialValidationResult(
            GifProviderException exception)
    {
        CredentialValidationFailure failure =
            exception.Failure switch
            {
                GifProviderFailure.MissingCredential =>
                    CredentialValidationFailure.MissingCredential,

                GifProviderFailure.Unauthorized =>
                    CredentialValidationFailure.InvalidCredential,

                GifProviderFailure.RateLimited =>
                    CredentialValidationFailure.RateLimited,

                GifProviderFailure.Network =>
                    CredentialValidationFailure.Network,

                GifProviderFailure.Timeout =>
                    CredentialValidationFailure.Timeout,

                GifProviderFailure.ServiceUnavailable =>
                    CredentialValidationFailure.ServiceUnavailable,

                _ =>
                    CredentialValidationFailure.Unknown
            };

        string message =
            failure ==
                CredentialValidationFailure.InvalidCredential
                ? "KLIPY did not accept that API key."
                : exception.Message;

        return CredentialValidationResult.Invalid(
            message,
            failure);
    }

    private static GifProviderException
        InvalidResponse(
            string message)
    {
        return new GifProviderException(
            ProviderId,
            GifProviderFailure.InvalidResponse,
            message);
    }
}
