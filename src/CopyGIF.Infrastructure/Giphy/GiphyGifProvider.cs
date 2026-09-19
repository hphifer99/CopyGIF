using System.Globalization;
using System.Net;
using System.Text.Json;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;
using CopyGIF.Core.Settings;

namespace CopyGIF.Infrastructure.Giphy;

public sealed class GiphyGifProvider(HttpClient httpClient, ISecretStore secrets) : IGifProvider
{
    public const string ProviderId = "giphy";
    public string Id => ProviderId;
    public string DisplayName => "GIPHY";

    public async Task<GifSearchPage> SearchAsync(GifSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? key = await secrets.GetAsync(SecretNames.GiphyApiKey, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(key)) throw Failure(GifProviderFailure.MissingCredential, "Add your GIPHY API key in Settings.");
        if (request.Kind == GifSearchKind.Search && request.Query.Length > 50)
            throw Failure(GifProviderFailure.InvalidResponse, "GIPHY searches can contain at most 50 characters.");
        int offset = 0;
        int maximumOffset = request.Kind == GifSearchKind.Trending ? 499 : 4999;
        if (request.ContinuationToken is string token &&
            (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset > maximumOffset))
            throw Failure(GifProviderFailure.InvalidResponse, "The GIPHY page token is invalid.");
        int limit = Math.Clamp(request.PageSize, 1, 50);
        using JsonDocument document = await RequestAsync(key, request.Kind, request.Query, limit, offset,
            request.ContentRating, cancellationToken).ConfigureAwait(false);
        try
        {
            JsonElement root = document.RootElement;
            var items = new List<GifItem>();
            foreach (JsonElement entry in root.GetProperty("data").EnumerateArray())
            {
                try { items.Add(Map(entry)); }
                catch (Exception exception) when (exception is GifProviderException or JsonException or
                    KeyNotFoundException or InvalidOperationException or FormatException)
                {
                    RepairDiagnostics.Record("provider-item", Id, "invalid");
                }
            }
            JsonElement pagination = root.GetProperty("pagination");
            int count = pagination.GetProperty("count").GetInt32();
            int responseOffset = pagination.GetProperty("offset").GetInt32();
            int total = pagination.TryGetProperty("total_count", out var totalValue) ? totalValue.GetInt32() : int.MaxValue;
            int next = checked(responseOffset + count);
            return new GifSearchPage
            {
                Items = items, TotalCount = total == int.MaxValue ? null : total,
                ContinuationToken = count > 0 && next > offset && next < total && next <= maximumOffset
                    ? next.ToString(CultureInfo.InvariantCulture) : null
            };
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or
            InvalidOperationException or FormatException or OverflowException)
        {
            throw Failure(GifProviderFailure.InvalidResponse, "GIPHY returned an unsupported response.");
        }
    }

    public async Task<CredentialValidationResult> ValidateCredentialAsync(string credential, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credential))
            return CredentialValidationResult.Invalid("Enter a GIPHY API key.", CredentialValidationFailure.MissingCredential);
        try
        {
            using var response = await RequestAsync(credential.Trim(), GifSearchKind.Trending, string.Empty, 1, 0,
                GifContentRating.AllAvailable, cancellationToken).ConfigureAwait(false);
            return CredentialValidationResult.Valid();
        }
        catch (GifProviderException exception)
        {
            return CredentialValidationResult.Invalid(exception.Message, exception.Failure switch
            {
                GifProviderFailure.Unauthorized => CredentialValidationFailure.InvalidCredential,
                GifProviderFailure.RateLimited => CredentialValidationFailure.RateLimited,
                GifProviderFailure.Timeout => CredentialValidationFailure.Timeout,
                GifProviderFailure.Network => CredentialValidationFailure.Network,
                GifProviderFailure.ServiceUnavailable => CredentialValidationFailure.ServiceUnavailable,
                _ => CredentialValidationFailure.Unknown
            });
        }
    }

    // GIPHY analytics are optional. No persistent user tracking identifier is created.
    public Task RegisterShareAsync(string itemId, string? searchQuery, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private async Task<JsonDocument> RequestAsync(string key, GifSearchKind kind, string query,
        int limit, int offset, GifContentRating rating, CancellationToken cancellationToken)
    {
        string endpoint = kind == GifSearchKind.Trending ? "trending" : "search";
        string url = $"https://api.giphy.com/v1/gifs/{endpoint}?api_key={Uri.EscapeDataString(key)}&limit={limit}&offset={offset}";
        if (kind == GifSearchKind.Search) url += "&q=" + Uri.EscapeDataString(query);
        string? ratingValue = rating switch
        {
            GifContentRating.AllAvailable => null,
            GifContentRating.G => "g",
            GifContentRating.Pg => "pg",
            GifContentRating.Pg13 => "pg-13",
            GifContentRating.R => "r",
            _ => throw new ArgumentOutOfRangeException(nameof(rating))
        };
        if (ratingValue is not null) url += "&rating=" + ratingValue;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(25));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => Failure(GifProviderFailure.Unauthorized, "GIPHY rejected this API key."),
                    HttpStatusCode.TooManyRequests => Failure(GifProviderFailure.RateLimited, "GIPHY's request limit has been reached. Wait before trying again."),
                    _ => Failure(GifProviderFailure.ServiceUnavailable, $"GIPHY returned HTTP {(int)response.StatusCode}.")
                };
            const int maximumBytes = 8 * 1024 * 1024;
            if (response.Content.Headers.ContentLength > maximumBytes)
                throw Failure(GifProviderFailure.InvalidResponse, "The GIPHY response exceeded the size limit.");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            using var data = new MemoryStream();
            byte[] buffer = new byte[32768];
            int count;
            while ((count = await stream.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) != 0)
            {
                if (data.Length + count > maximumBytes) throw Failure(GifProviderFailure.InvalidResponse, "The GIPHY response exceeded the size limit.");
                data.Write(buffer, 0, count);
            }
            JsonDocument document = JsonDocument.Parse(data.ToArray(), new JsonDocumentOptions { MaxDepth = 32 });
            if (!document.RootElement.TryGetProperty("meta", out var meta) ||
                !meta.TryGetProperty("status", out var status) || status.GetInt32() != 200)
            {
                document.Dispose();
                throw Failure(GifProviderFailure.InvalidResponse, "GIPHY returned an unsuccessful response.");
            }
            RepairDiagnostics.Record(kind == GifSearchKind.Trending ? "trending-request" : "search-request", Id, "success", data.Length);
            return document;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw Failure(GifProviderFailure.Timeout, "GIPHY did not respond in time."); }
        catch (HttpRequestException)
        { throw Failure(GifProviderFailure.Network, "GIPHY could not be reached."); }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or OverflowException)
        { throw Failure(GifProviderFailure.InvalidResponse, "GIPHY returned invalid JSON."); }
    }

    private static GifItem Map(JsonElement data)
    {
        JsonElement images = data.GetProperty("images");
        JsonElement original = images.GetProperty("original");
        JsonElement preview = images.TryGetProperty("fixed_width", out var small) ? small : original;
        JsonElement still = images.TryGetProperty("fixed_width_still", out var thumbnail) ? thumbnail : images.GetProperty("original_still");
        Uri Media(JsonElement rendition)
        {
            if (!Uri.TryCreate(rendition.GetProperty("url").GetString(), UriKind.Absolute, out var uri) ||
                !ProviderMediaPolicy.IsDirectMediaUri(uri))
                throw Failure(GifProviderFailure.InvalidResponse, "GIPHY returned an unsupported media address.");
            return uri;
        }
        Uri? OptionalGif(string name)
        {
            if (!images.TryGetProperty(name, out JsonElement rendition) ||
                !rendition.TryGetProperty("url", out JsonElement url) ||
                !Uri.TryCreate(url.GetString(), UriKind.Absolute, out Uri? uri) ||
                !uri.AbsolutePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                !ProviderMediaPolicy.IsDirectMediaUri(uri)) return null;
            return uri;
        }
        int Number(JsonElement item, string name) => item.TryGetProperty(name, out var value) &&
            int.TryParse(value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : 0;
        string id = data.GetProperty("id").GetString() ?? throw Failure(GifProviderFailure.InvalidResponse, "GIPHY returned a GIF without an ID.");
        Uri? sourcePage = null;
        if (data.TryGetProperty("url", out var page) && Uri.TryCreate(page.GetString(), UriKind.Absolute, out var pageUri) &&
            pageUri.Scheme == "https" && pageUri.IdnHost == "giphy.com" && pageUri.IsDefaultPort && string.IsNullOrEmpty(pageUri.UserInfo)) sourcePage = pageUri;
        return new GifItem
        {
            ProviderId = ProviderId, Id = id, Title = data.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
            ThumbnailUri = Media(still), PreviewUri = Media(preview), GifUri = Media(original), SourcePageUri = sourcePage,
            Renditions = new GifRenditions
            {
                Minimum = OptionalGif("fixed_width_small"),
                Low = OptionalGif("fixed_width"),
                Medium = OptionalGif("fixed_height"),
                High = OptionalGif("downsized_large"),
                Maximum = Media(original)
            },
            Width = Number(original, "width"), Height = Number(original, "height")
        };
    }

    private static GifProviderException Failure(GifProviderFailure failure, string message) => new(ProviderId, failure, message);
}
