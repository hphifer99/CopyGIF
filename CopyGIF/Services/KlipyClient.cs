using CopyGIF.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CopyGIF.Services
{
    public sealed class KlipyClient
    {
        private const string ApiBaseUrl =
            "https://api.klipy.com/api/v1/";

        private readonly HttpClient _httpClient;

        public KlipyClient(HttpClient httpClient)
        {
            _httpClient = httpClient
                ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<IReadOnlyList<GifItem>> SearchAsync(
            string apiKey,
            string query,
            int resultsPerPage,
            CancellationToken cancellationToken)
        {
            ValidateApiKeyValue(apiKey);

            if (string.IsNullOrWhiteSpace(query))
            {
                return new List<GifItem>();
            }

            if (resultsPerPage < 1 || resultsPerPage > 50)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resultsPerPage),
                    "Results per page must be between 1 and 50.");
            }

            string requestUrl =
                ApiBaseUrl +
                Uri.EscapeDataString(apiKey.Trim()) +
                "/gifs/search?q=" +
                Uri.EscapeDataString(query.Trim()) +
                "&page=1&per_page=" + resultsPerPage;

            KlipyResponse apiResponse = await SendAsync(
                requestUrl,
                cancellationToken);

            if (!apiResponse.Result)
            {
                throw new InvalidOperationException(
                    "KLIPY rejected the search request.");
            }

            if (apiResponse.Data?.Items == null)
            {
                return new List<GifItem>();
            }

            return apiResponse.Data.Items
                .Select(CreateGifItem)
                .Where(item => item != null)
                .ToList();
        }

        public async Task ValidateApiKeyAsync(
            string apiKey,
            CancellationToken cancellationToken)
        {
            ValidateApiKeyValue(apiKey);

            string requestUrl =
                ApiBaseUrl +
                Uri.EscapeDataString(apiKey.Trim()) +
                "/gifs/trending?page=1&per_page=1";

            KlipyResponse apiResponse = await SendAsync(
                requestUrl,
                cancellationToken);

            if (!apiResponse.Result)
            {
                throw new UnauthorizedAccessException(
                    "KLIPY did not accept that API key.");
            }
        }

        private async Task<KlipyResponse> SendAsync(
            string requestUrl,
            CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response =
                await _httpClient.GetAsync(
                    requestUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden ||
                    response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new UnauthorizedAccessException(
                        "KLIPY did not accept that API key.");
                }

                if ((int)response.StatusCode == 429)
                {
                    throw new KlipyRateLimitException();
                }

                response.EnsureSuccessStatusCode();

                using (var responseStream =
                    await response.Content.ReadAsStreamAsync())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var serializer =
                        new DataContractJsonSerializer(typeof(KlipyResponse));

                    return serializer.ReadObject(responseStream)
                        as KlipyResponse
                        ?? new KlipyResponse();
                }
            }
        }

        private static void ValidateApiKeyValue(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException(
                    "A KLIPY API key is required.",
                    nameof(apiKey));
            }
        }

        private static GifItem CreateGifItem(KlipyGif source)
        {
            KlipyMedia fullGif =
                source?.Files?.Hd?.Gif ??
                source?.Files?.Md?.Gif ??
                source?.Files?.Sm?.Gif;

            KlipyMedia thumbnail =
                source?.Files?.Sm?.Jpg ??
                source?.Files?.Md?.Jpg ??
                source?.Files?.Hd?.Jpg;

            KlipyMedia previewGif =
                source?.Files?.Sm?.Gif ??
                source?.Files?.Md?.Gif ??
                fullGif;

            if (source == null ||
                fullGif == null ||
                thumbnail == null ||
                previewGif == null ||
                string.IsNullOrWhiteSpace(fullGif.Url) ||
                string.IsNullOrWhiteSpace(thumbnail.Url) ||
                string.IsNullOrWhiteSpace(previewGif.Url))
            {
                return null;
            }

            return new GifItem
            {
                Id = source.Id,
                Title = source.Title ?? string.Empty,
                ThumbnailUrl = thumbnail.Url,
                FullGifUrl = fullGif.Url,
                PreviewGifUrl = previewGif.Url,
                Width = fullGif.Width,
                Height = fullGif.Height
            };
        }
    }

    internal sealed class KlipyRateLimitException : Exception
    {
        public KlipyRateLimitException()
            : base("KLIPY rate limited the request. Wait and try again.")
        {
        }
    }

    [DataContract]
    internal sealed class KlipyResponse
    {
        [DataMember(Name = "result")]
        public bool Result { get; set; }

        [DataMember(Name = "data")]
        public KlipyPage Data { get; set; }
    }

    [DataContract]
    internal sealed class KlipyPage
    {
        [DataMember(Name = "data")]
        public List<KlipyGif> Items { get; set; }
    }

    [DataContract]
    internal sealed class KlipyGif
    {
        [DataMember(Name = "id")]
        public long Id { get; set; }

        [DataMember(Name = "title")]
        public string Title { get; set; }

        [DataMember(Name = "file")]
        public KlipyFiles Files { get; set; }
    }

    [DataContract]
    internal sealed class KlipyFiles
    {
        [DataMember(Name = "hd")]
        public KlipyFormats Hd { get; set; }

        [DataMember(Name = "md")]
        public KlipyFormats Md { get; set; }

        [DataMember(Name = "sm")]
        public KlipyFormats Sm { get; set; }
    }

    [DataContract]
    internal sealed class KlipyFormats
    {
        [DataMember(Name = "gif")]
        public KlipyMedia Gif { get; set; }

        [DataMember(Name = "jpg")]
        public KlipyMedia Jpg { get; set; }
    }

    [DataContract]
    internal sealed class KlipyMedia
    {
        [DataMember(Name = "url")]
        public string Url { get; set; }

        [DataMember(Name = "width")]
        public int Width { get; set; }

        [DataMember(Name = "height")]
        public int Height { get; set; }
    }
}
