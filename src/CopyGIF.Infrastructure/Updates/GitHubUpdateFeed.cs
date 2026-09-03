using System.Net;
using System.Net.Http.Headers;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;

namespace CopyGIF.Infrastructure.Updates;

public sealed class GitHubUpdateFeed :
    IUpdateFeed
{
    private const int MaximumRedirects = 5;

    private static readonly Uri LatestManifestUri =
        new(
            "https://github.com/hphifer99/CopyGIF/releases/latest/download/CopyGIF-update.json");

    private readonly HttpClient _httpClient;

    private readonly UpdateManifestParser _parser;

    public GitHubUpdateFeed(
        HttpClient httpClient,
        UpdateManifestParser parser)
    {
        _httpClient =
            httpClient ??
            throw new ArgumentNullException(
                nameof(httpClient));

        _parser =
            parser ??
            throw new ArgumentNullException(
                nameof(parser));
    }

    public async Task<UpdateManifest?> GetLatestAsync(
        string channel,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            channel);

        if (!string.Equals(
                channel,
                "stable",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentOutOfRangeException(
                nameof(channel),
                channel,
                "Only the stable update channel is supported.");
        }

        Uri currentUri = LatestManifestUri;

        for (int redirectCount = 0;
             redirectCount <= MaximumRedirects;
             redirectCount++)
        {
            UpdateManifestParser
                .EnsureAllowedTransportUri(
                    currentUri);

            using HttpRequestMessage request =
                new(
                    HttpMethod.Get,
                    currentUri);

            request.Headers.UserAgent.Add(
                new ProductInfoHeaderValue(
                    "CopyGIF",
                    "2.0"));

            request.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    "application/json"));

            using HttpResponseMessage response =
                await _httpClient
                    .SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (response.StatusCode ==
                HttpStatusCode.NotFound)
            {
                return null;
            }

            if (IsRedirect(
                    response.StatusCode))
            {
                if (redirectCount == MaximumRedirects)
                {
                    throw new HttpRequestException(
                        "The update manifest exceeded the redirect limit.");
                }

                currentUri = ResolveRedirect(
                    currentUri,
                    response.Headers.Location);

                continue;
            }

            response.EnsureSuccessStatusCode();

            long? declaredLength =
                response.Content.Headers.ContentLength;

            if (declaredLength is < 0 or
                > StoragePolicy.MaximumUpdateManifestBytes)
            {
                throw new InvalidDataException(
                    "The update manifest exceeds the allowed size.");
            }

            byte[] content =
                await ReadBoundedAsync(
                        response.Content,
                        StoragePolicy.MaximumUpdateManifestBytes,
                        cancellationToken)
                    .ConfigureAwait(false);

            return _parser.Parse(
                content,
                channel);
        }

        throw new InvalidOperationException(
            "The update-manifest request ended unexpectedly.");
    }

    private static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        await using Stream source =
            await content
                .ReadAsStreamAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        using MemoryStream destination =
            new();

        byte[] buffer = new byte[16 * 1024];
        long totalBytes = 0;

        while (true)
        {
            int bytesRead =
                await source
                    .ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;

            if (totalBytes > maximumBytes)
            {
                throw new InvalidDataException(
                    "The update manifest exceeds the allowed size.");
            }

            await destination
                .WriteAsync(
                    buffer.AsMemory(
                        0,
                        bytesRead),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return destination.ToArray();
    }

    private static bool IsRedirect(
        HttpStatusCode statusCode)
    {
        return statusCode is
            HttpStatusCode.Moved or
            HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;
    }

    private static Uri ResolveRedirect(
        Uri currentUri,
        Uri? location)
    {
        if (location is null)
        {
            throw new HttpRequestException(
                "The update manifest redirect did not include a destination.");
        }

        Uri resolved = location.IsAbsoluteUri
            ? location
            : new Uri(
                currentUri,
                location);

        UpdateManifestParser
            .EnsureAllowedTransportUri(
                resolved);

        return resolved;
    }
}
