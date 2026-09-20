using System.Globalization;
using CopyGIF.Core.Models;

namespace CopyGIF.Infrastructure.Klipy;

internal static class KlipyResponseMapper
{
    public static GifSearchPage Map(
        KlipyResponseDto response)
    {
        ArgumentNullException.ThrowIfNull(
            response);

        if (!response.Result)
        {
            throw new GifProviderException(
                KlipyGifProvider.ProviderId,
                GifProviderFailure.Unauthorized,
                "KLIPY rejected the request.");
        }

        KlipyPageDto page =
            response.Data ??
            throw InvalidResponse(
                "KLIPY did not return a data page.");

        if (page.CurrentPage < 1 ||
            page.PerPage < 1 ||
            page.PerPage > 50)
        {
            throw InvalidResponse(
                "KLIPY returned invalid pagination data.");
        }

        List<KlipyItemDto?> sourceItems =
            page.Items ??
            throw InvalidResponse(
                "KLIPY returned a null result collection.");

        List<GifItem> items =
            new(sourceItems.Count);

        foreach (KlipyItemDto? item
                 in sourceItems)
        {
            if (item is null)
            {
                RepairDiagnostics.Record("provider-item", KlipyGifProvider.ProviderId, "null");
                continue;
            }
            try { items.Add(MapItem(item)); }
            catch (GifProviderException)
            {
                RepairDiagnostics.Record("provider-item", KlipyGifProvider.ProviderId, "invalid");
            }
        }

        string? continuationToken = null;

        if (page.HasNext)
        {
            if (page.CurrentPage == int.MaxValue)
            {
                throw InvalidResponse(
                    "KLIPY returned an invalid final page.");
            }

            continuationToken =
                (page.CurrentPage + 1)
                .ToString(
                    CultureInfo.InvariantCulture);
        }

        return new GifSearchPage
        {
            Items = items,
            ContinuationToken =
                continuationToken
        };
    }

    private static GifItem MapItem(
        KlipyItemDto item)
    {
        if (string.IsNullOrWhiteSpace(
                item.Slug))
        {
            throw InvalidResponse(
                "A KLIPY result did not contain a slug.");
        }

        KlipyFilesDto files =
            item.Files ??
            throw InvalidResponse(
                "A KLIPY result did not contain media files.");

        KlipyMediaDto fullGif =
            files.Hd?.Gif ??
            files.Md?.Gif ??
            files.Sm?.Gif ??
            files.Xs?.Gif ??
            throw InvalidResponse(
                "A KLIPY result did not contain a GIF rendition.");

        KlipyMediaDto preview =
            files.Sm?.Gif ??
            files.Md?.Gif ??
            files.Xs?.Gif ??
            fullGif;

        KlipyMediaDto thumbnail =
            files.Sm?.Jpg ??
            files.Xs?.Jpg ??
            files.Md?.Jpg ??
            files.Hd?.Jpg ??
            preview;

        Uri gifUri = ParseHttpsUri(
            fullGif.Url,
            "GIF");

        Uri previewUri = ParseHttpsUri(
            preview.Url,
            "preview");

        Uri thumbnailUri = ParseHttpsUri(
            thumbnail.Url,
            "thumbnail");

        if (fullGif.Width < 1 ||
            fullGif.Height < 1 ||
            fullGif.Size < 0)
        {
            throw InvalidResponse(
                "KLIPY returned invalid GIF dimensions or size.");
        }

        return new GifItem
        {
            ProviderId =
                KlipyGifProvider.ProviderId,

            Id = item.Slug.Trim(),

            Title = item.Title?.Trim() ??
                string.Empty,

            Description = string.Empty,

            ThumbnailUri = thumbnailUri,

            GifUri = gifUri,

            Renditions = new GifRenditions
            {
                Minimum = TryGif(files.Xs?.Gif),
                Low = TryGif(files.Sm?.Gif),
                Medium = TryGif(files.Md?.Gif),
                High = TryGif(files.Hd?.Gif),
                Maximum = gifUri
            },

            PreviewUri = previewUri,

            Width = fullGif.Width,

            Height = fullGif.Height,

            SizeBytes =
                fullGif.Size == 0
                    ? null
                    : fullGif.Size
        };
    }

    private static Uri ParseHttpsUri(
        string? value,
        string mediaName)
    {
        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            throw InvalidResponse(
                $"KLIPY returned an invalid {mediaName} URL.");
        }

        return uri;
    }

    private static Uri? TryGif(KlipyMediaDto? media)
    {
        if (media is null || media.Width < 1 || media.Height < 1 ||
            !Uri.TryCreate(media.Url, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !uri.AbsolutePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            return null;
        return uri;
    }

    private static GifProviderException
        InvalidResponse(
            string message)
    {
        return new GifProviderException(
            KlipyGifProvider.ProviderId,
            GifProviderFailure.InvalidResponse,
            message);
    }
}
