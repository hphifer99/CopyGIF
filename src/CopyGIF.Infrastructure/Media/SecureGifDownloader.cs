using System.Net;
using System.Security.Cryptography;
using System.Text;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;
using CopyGIF.Core.Settings;
using CopyGIF.Infrastructure.Storage;

namespace CopyGIF.Infrastructure.Media;

public sealed class SecureGifDownloader :
    IGifDownloader
{
    private readonly HttpClient _httpClient;

    private readonly MediaHostPolicy
        _hostPolicy;

    private readonly IApplicationPaths
        _paths;

    private readonly ISettingsStore
        _settingsStore;

    private readonly IClock _clock;

    private readonly OwnedPathGuard
        _pathGuard;

    public SecureGifDownloader(
        HttpClient httpClient,
        MediaHostPolicy hostPolicy,
        IApplicationPaths paths,
        ISettingsStore settingsStore,
        IClock clock,
        OwnedPathGuard pathGuard)
    {
        _httpClient =
            httpClient ??
            throw new ArgumentNullException(
                nameof(httpClient));

        _hostPolicy =
            hostPolicy ??
            throw new ArgumentNullException(
                nameof(hostPolicy));

        _paths =
            paths ??
            throw new ArgumentNullException(
                nameof(paths));

        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));

        _pathGuard =
            pathGuard ??
            throw new ArgumentNullException(
                nameof(pathGuard));
    }

    public async Task<DownloadedGif> DownloadAsync(
        GifItem item,
        GifDownloadPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            item);

        if (!Enum.IsDefined(
                purpose))
        {
            throw new ArgumentOutOfRangeException(
                nameof(purpose),
                purpose,
                "The GIF download purpose is not supported.");
        }

        DownloadDestination destination =
            await GetDestinationAsync(
                    purpose,
                    cancellationToken)
                .ConfigureAwait(false);

        _pathGuard.EnsureSafeDirectory(
            destination.OwnedRoot,
            destination.Directory);

        string fileName =
            CreateStableFileName(
                item.StableIdentity);

        string finalPath =
            _pathGuard.EnsureSafeFilePath(
                destination.OwnedRoot,
                Path.Combine(
                    destination.Directory,
                    fileName));

        string temporaryPath =
            _pathGuard.EnsureSafeFilePath(
                destination.OwnedRoot,
                Path.Combine(
                    destination.Directory,
                    $".{fileName}.{Guid.NewGuid():N}.tmp"));

        try
        {
            DownloadResult result =
                await DownloadToFileAsync(
                        item.GifUri,
                        temporaryPath,
                        cancellationToken)
                    .ConfigureAwait(false);

            _pathGuard.EnsureSafeFilePath(
                destination.OwnedRoot,
                finalPath);

            File.Move(
                temporaryPath,
                finalPath,
                overwrite: true);

            return new DownloadedGif
            {
                Identity =
                    item.StableIdentity,

                SourceUri =
                    result.SourceUri,

                FilePath =
                    finalPath,

                SizeBytes =
                    result.SizeBytes,

                Sha256 =
                    result.Sha256,

                DownloadedAtUtc =
                    _clock.UtcNow,

                Purpose = purpose
            };
        }
        catch (OperationCanceledException)
        {
            DeleteTemporaryFile(
                destination.OwnedRoot,
                temporaryPath);

            throw;
        }
        catch (MediaDownloadException)
        {
            DeleteTemporaryFile(
                destination.OwnedRoot,
                temporaryPath);

            throw;
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
            DeleteTemporaryFile(
                destination.OwnedRoot,
                temporaryPath);

            throw new MediaDownloadException(
                MediaDownloadFailure.Storage,
                "The GIF could not be stored safely.",
                exception);
        }
    }

    private async Task<DownloadDestination>
        GetDestinationAsync(
            GifDownloadPurpose purpose,
            CancellationToken cancellationToken)
    {
        if (purpose ==
            GifDownloadPurpose.Clipboard)
        {
            return new DownloadDestination(
                _paths.CacheDirectory,
                _paths.ClipboardCacheDirectory);
        }

        AppSettings settings =
            AppSettingsNormalizer.Normalize(
                await _settingsStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false));

        string? customRoot =
            settings.Library.CustomStorageRoot;

        string ownedRoot =
            _paths.GetLibraryRoot(
                customRoot);

        string directory =
            purpose switch
            {
                GifDownloadPurpose.Favorite =>
                    _paths.GetFavoritesDirectory(
                        customRoot),

                GifDownloadPurpose.Recent =>
                    _paths.GetRecentsDirectory(
                        customRoot),

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(purpose),
                        purpose,
                        "The GIF download purpose is not supported.")
            };

        return new DownloadDestination(
            ownedRoot,
            directory);
    }

    private async Task<DownloadResult>
        DownloadToFileAsync(
            Uri initialUri,
            string temporaryPath,
            CancellationToken cancellationToken)
    {
        Uri currentUri = initialUri;
        int redirectCount = 0;

        while (true)
        {
            await _hostPolicy
                .ValidateAsync(
                    currentUri,
                    cancellationToken)
                .ConfigureAwait(false);

            using HttpRequestMessage request =
                new(
                    HttpMethod.Get,
                    currentUri);

            using HttpResponseMessage response =
                await SendAsync(
                        request,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (IsRedirect(
                    response.StatusCode))
            {
                if (redirectCount >=
                    MediaPolicy.MaximumRedirects)
                {
                    throw new MediaDownloadException(
                        MediaDownloadFailure.RedirectLimitExceeded,
                        "The media download exceeded the redirect limit.");
                }

                Uri? location =
                    response.Headers.Location;

                if (location is null ||
                    !Uri.TryCreate(
                        currentUri,
                        location,
                        out Uri? nextUri))
                {
                    throw new MediaDownloadException(
                        MediaDownloadFailure.InvalidUri,
                        "The media server returned an invalid redirect.");
                }

                currentUri = nextUri;
                redirectCount++;

                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new MediaDownloadException(
                    MediaDownloadFailure.HttpError,
                    $"The media server returned HTTP {(int)response.StatusCode}.",
                    httpStatusCode:
                        (int)response.StatusCode);
            }

            long? contentLength =
                response.Content.Headers
                    .ContentLength;

            if (contentLength >
                MediaPolicy.MaximumGifBytes)
            {
                throw new MediaDownloadException(
                    MediaDownloadFailure.TooLarge,
                    "The GIF exceeds the 50 MiB download limit.");
            }

            return await StreamToFileAsync(
                    response,
                    currentUri,
                    temporaryPath,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
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
            throw new MediaDownloadException(
                MediaDownloadFailure.Timeout,
                "The GIF download timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new MediaDownloadException(
                MediaDownloadFailure.Network,
                "The GIF could not be downloaded.",
                exception);
        }
    }

    private static async Task<DownloadResult>
        StreamToFileAsync(
            HttpResponseMessage response,
            Uri sourceUri,
            string temporaryPath,
            CancellationToken cancellationToken)
    {
        await using Stream source =
            await response.Content
                .ReadAsStreamAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        await using FileStream destination =
            new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                options:
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);

        using IncrementalHash hash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        byte[] buffer =
            new byte[81920];

        byte[] header =
            new byte[13];

        int headerLength = 0;
        long totalBytes = 0;

        while (true)
        {
            int read =
                await source.ReadAsync(
                        buffer,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            if (totalBytes + read >
                MediaPolicy.MaximumGifBytes)
            {
                throw new MediaDownloadException(
                    MediaDownloadFailure.TooLarge,
                    "The GIF exceeds the 50 MiB download limit.");
            }

            int headerBytesNeeded =
                header.Length -
                headerLength;

            if (headerBytesNeeded > 0)
            {
                int headerBytesToCopy =
                    Math.Min(
                        read,
                        headerBytesNeeded);

                buffer.AsSpan(
                        0,
                        headerBytesToCopy)
                    .CopyTo(
                        header.AsSpan(
                            headerLength));

                headerLength +=
                    headerBytesToCopy;
            }

            hash.AppendData(
                buffer,
                0,
                read);

            await destination.WriteAsync(
                    buffer.AsMemory(
                        0,
                        read),
                    cancellationToken)
                .ConfigureAwait(false);

            totalBytes += read;
        }

        await destination.FlushAsync(
                cancellationToken)
            .ConfigureAwait(false);

        if (!HasValidGifHeader(
                header,
                headerLength))
        {
            throw new MediaDownloadException(
                MediaDownloadFailure.InvalidGif,
                "The downloaded file is not a valid GIF.");
        }

        string sha256 =
            Convert.ToHexString(
                    hash.GetHashAndReset())
                .ToLowerInvariant();

        return new DownloadResult(
            sourceUri,
            totalBytes,
            sha256);
    }

    private static bool HasValidGifHeader(
        byte[] header,
        int headerLength)
    {
        if (headerLength < 13)
        {
            return false;
        }

        bool hasSignature =
            header.AsSpan(
                    0,
                    6)
                .SequenceEqual(
                    "GIF87a"u8) ||
            header.AsSpan(
                    0,
                    6)
                .SequenceEqual(
                    "GIF89a"u8);

        int width =
            header[6] |
            (header[7] << 8);

        int height =
            header[8] |
            (header[9] << 8);

        return hasSignature &&
               width > 0 &&
               height > 0;
    }

    private static bool IsRedirect(
        HttpStatusCode statusCode)
    {
        return statusCode is
            HttpStatusCode.MovedPermanently or
            HttpStatusCode.Redirect or
            HttpStatusCode.RedirectMethod or
            HttpStatusCode.TemporaryRedirect or
            HttpStatusCode.PermanentRedirect;
    }

    private static string CreateStableFileName(
        GifIdentity identity)
    {
        byte[] identityBytes =
            Encoding.UTF8.GetBytes(
                identity.ToString());

        byte[] identityHash =
            SHA256.HashData(
                identityBytes);

        return Convert.ToHexString(
                identityHash)
            .ToLowerInvariant() +
            ".gif";
    }

    private void DeleteTemporaryFile(
        string ownedRoot,
        string temporaryPath)
    {
        try
        {
            _pathGuard.DeleteOwnedFileIfPresent(
                ownedRoot,
                temporaryPath);
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  MediaDownloadException)
        {
        }
    }

    private sealed record DownloadDestination(
        string OwnedRoot,
        string Directory);

    private sealed record DownloadResult(
        Uri SourceUri,
        long SizeBytes,
        string Sha256);
}
