using System.Globalization;
using CopyGIF.Core.Models;

namespace CopyGIF.Infrastructure.Klipy;

internal static class KlipyRequestBuilder
{
    private const int MinimumPageSize = 8;

    private const int MaximumPageSize = 50;

    public static string BuildSearch(
        string apiKey,
        GifSearchRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            apiKey);

        ArgumentNullException.ThrowIfNull(
            request);

        int page = ParsePage(
            request.ContinuationToken);

        int pageSize = Math.Clamp(
            request.PageSize,
            MinimumPageSize,
            MaximumPageSize);

        string endpoint =
            request.Kind switch
            {
                GifSearchKind.Search =>
                    "gifs/search",

                GifSearchKind.Trending =>
                    "gifs/trending",

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(request),
                        request.Kind,
                        "The search kind is not supported.")
            };

        string uri =
            "api/v1/" +
            Escape(apiKey.Trim()) +
            "/" +
            endpoint +
            "?page=" +
            page.ToString(
                CultureInfo.InvariantCulture) +
            "&per_page=" +
            pageSize.ToString(
                CultureInfo.InvariantCulture);

        if (request.Kind ==
            GifSearchKind.Search)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                request.Query);

            uri +=
                "&q=" +
                Escape(request.Query.Trim());
        }

        return uri;
    }

    public static string BuildCredentialValidation(
        string apiKey)
    {
        return BuildSearch(
            apiKey,
            new GifSearchRequest
            {
                Query = string.Empty,
                Kind = GifSearchKind.Trending,
                PageSize = MinimumPageSize
            });
    }

    public static string BuildShare(
        string apiKey,
        string itemSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            apiKey);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            itemSlug);

        return
            "api/v1/" +
            Escape(apiKey.Trim()) +
            "/gifs/share/" +
            Escape(itemSlug.Trim());
    }

    private static int ParsePage(
        string? continuationToken)
    {
        if (string.IsNullOrWhiteSpace(
                continuationToken))
        {
            return 1;
        }

        if (!int.TryParse(
                continuationToken,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int page) ||
            page < 1)
        {
            throw new ArgumentException(
                "The continuation token is not a valid KLIPY page.",
                nameof(continuationToken));
        }

        return page;
    }

    private static string Escape(
        string value)
    {
        return Uri.EscapeDataString(
            value);
    }
}
