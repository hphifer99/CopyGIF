using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Infrastructure.Storage;

namespace CopyGIF.Infrastructure.Updates;

public sealed class HttpUpdatePackageService :
    IUpdatePackageService
{
    private const int MaximumRedirects = 5;

    private const int CopyBufferSize =
        128 * 1024;

    private readonly HttpClient _httpClient;

    private readonly IApplicationPaths _paths;

    private readonly OwnedPathGuard _pathGuard;

    private readonly IClock _clock;

    public HttpUpdatePackageService(
        HttpClient httpClient,
        IApplicationPaths paths,
        OwnedPathGuard pathGuard,
        IClock clock)
    {
        _httpClient =
            httpClient ??
            throw new ArgumentNullException(
                nameof(httpClient));

        _paths =
            paths ??
            throw new ArgumentNullException(
                nameof(paths));

        _pathGuard =
            pathGuard ??
            throw new ArgumentNullException(
                nameof(pathGuard));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));
    }

    public async Task<DownloadedUpdatePackage> DownloadAsync(
        UpdateManifest manifest,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        UpdateManifestParser.Validate(
            manifest,
            expectedChannel: "stable");

        _paths.EnsureDirectoriesExist();

        string ownedRoot =
            Path.GetFullPath(
                _paths.UpdatesDirectory);

        _pathGuard.EnsureSafeDirectory(
            ownedRoot,
            ownedRoot);

        string finalPath =
            _pathGuard.EnsureSafeFilePath(
                ownedRoot,
                Path.Combine(
                    ownedRoot,
                    manifest.AssetName));

        string temporaryPath =
            _pathGuard.EnsureSafeFilePath(
                ownedRoot,
                Path.Combine(
                    ownedRoot,
                    $".{manifest.AssetName}.{Guid.NewGuid():N}.tmp"));

        try
        {
            DownloadResult result =
                await DownloadToTemporaryFileAsync(
                        manifest,
                        temporaryPath,
                        progress,
                        cancellationToken)
                    .ConfigureAwait(false);

            File.Move(
                temporaryPath,
                finalPath,
                overwrite: true);

            return new DownloadedUpdatePackage
            {
                Manifest = manifest,
                FilePath = finalPath,
                SizeBytes = result.SizeBytes,
                Sha256 = result.Sha256,
                DownloadedAtUtc = _clock.UtcNow
            };
        }
        catch
        {
            TryDeleteOwnedFile(
                ownedRoot,
                temporaryPath);

            throw;
        }
    }

    public Task DeleteAsync(
        DownloadedUpdatePackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            package);

        cancellationToken.ThrowIfCancellationRequested();

        string ownedRoot =
            Path.GetFullPath(
                _paths.UpdatesDirectory);

        string filePath =
            _pathGuard.EnsureSafeFilePath(
                ownedRoot,
                package.FilePath);

        _pathGuard.DeleteOwnedFileIfPresent(
            ownedRoot,
            filePath);

        return Task.CompletedTask;
    }

    private async Task<DownloadResult>
        DownloadToTemporaryFileAsync(
            UpdateManifest manifest,
            string temporaryPath,
            IProgress<UpdateDownloadProgress>? progress,
            CancellationToken cancellationToken)
    {
        Uri currentUri = manifest.AssetUri;

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
                    "application/octet-stream"));

            using HttpResponseMessage response =
                await _httpClient
                    .SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (IsRedirect(
                    response.StatusCode))
            {
                if (redirectCount == MaximumRedirects)
                {
                    throw new HttpRequestException(
                        "The update package exceeded the redirect limit.");
                }

                currentUri = ResolveRedirect(
                    currentUri,
                    response.Headers.Location);

                continue;
            }

            response.EnsureSuccessStatusCode();

            long? declaredLength =
                response.Content.Headers.ContentLength;

            if (declaredLength.HasValue &&
                declaredLength.Value !=
                    manifest.SizeBytes)
            {
                throw new InvalidDataException(
                    "The update package length does not match the manifest.");
            }

            return await WriteAndHashAsync(
                    response.Content,
                    manifest,
                    temporaryPath,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            "The update-package request ended unexpectedly.");
    }

    private static async Task<DownloadResult>
        WriteAndHashAsync(
            HttpContent content,
            UpdateManifest manifest,
            string temporaryPath,
            IProgress<UpdateDownloadProgress>? progress,
            CancellationToken cancellationToken)
    {
        await using Stream source =
            await content
                .ReadAsStreamAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        await using FileStream destination =
            new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                FileOptions.Asynchronous |
                FileOptions.WriteThrough);

        using IncrementalHash hasher =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        byte[] buffer = new byte[CopyBufferSize];
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

            if (totalBytes > manifest.SizeBytes ||
                totalBytes >
                    UpdateManifestParser.MaximumPackageBytes)
            {
                throw new InvalidDataException(
                    "The update package exceeds the size declared by the manifest.");
            }

            hasher.AppendData(
                buffer,
                0,
                bytesRead);

            await destination
                .WriteAsync(
                    buffer.AsMemory(
                        0,
                        bytesRead),
                    cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(
                new UpdateDownloadProgress
                {
                    BytesReceived = totalBytes,
                    TotalBytes = manifest.SizeBytes
                });
        }

        await destination
            .FlushAsync(
                cancellationToken)
            .ConfigureAwait(false);

        destination.Flush(
            flushToDisk: true);

        if (totalBytes != manifest.SizeBytes)
        {
            throw new InvalidDataException(
                "The update package is shorter than the size declared by the manifest.");
        }

        byte[] actualHash =
            hasher.GetHashAndReset();

        if (!UpdateManifestParser.TryParseSha256(
                manifest.Sha256,
                out byte[] expectedHash) ||
            !CryptographicOperations.FixedTimeEquals(
                expectedHash,
                actualHash))
        {
            throw new InvalidDataException(
                "The update package failed its SHA-256 integrity check.");
        }

        return new DownloadResult(
            totalBytes,
            Convert.ToHexString(
                    actualHash)
                .ToLowerInvariant());
    }

    private void TryDeleteOwnedFile(
        string ownedRoot,
        string filePath)
    {
        try
        {
            _pathGuard.DeleteOwnedFileIfPresent(
                ownedRoot,
                filePath);
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  MediaDownloadException)
        {
        }
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
                "The update package redirect did not include a destination.");
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

    private sealed record DownloadResult(
        long SizeBytes,
        string Sha256);
}
