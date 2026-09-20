using System.Net;
using System.Net.Http.Headers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
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

    // A partial download older than this is an orphan from a crash or a killed process.
    private static readonly TimeSpan StaleTemporaryFileAge =
        TimeSpan.FromHours(1);

    private static readonly Regex InstallerNamePattern =
        new(
            @"^CopyGIF-(?<major>\d{1,9})\.(?<minor>\d{1,9})\.(?<patch>\d{1,9})-win-x64\.msi$",
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

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

        using CancellationTokenSource deadline =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(600));
        cancellationToken = deadline.Token;

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

        // A package that was already downloaded for this exact manifest is reused
        // when its size and SHA-256 still match, so a pending update that the user
        // has not installed yet is not downloaded again on every check.
        DownloadedUpdatePackage? existing =
            await TryReuseExistingPackageAsync(
                    manifest,
                    finalPath,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

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

    public async Task<DownloadedUpdatePackage?>
        FindExistingAsync(
            UpdateManifest manifest,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            manifest);

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            UpdateManifestParser.Validate(
                manifest,
                expectedChannel: "stable");

            string ownedRoot =
                Path.GetFullPath(
                    _paths.UpdatesDirectory);

            if (!Directory.Exists(
                    ownedRoot))
            {
                return null;
            }

            _pathGuard.EnsureSafeDirectory(
                ownedRoot,
                ownedRoot);

            string finalPath =
                _pathGuard.EnsureSafeFilePath(
                    ownedRoot,
                    Path.Combine(
                        ownedRoot,
                        manifest.AssetName));

            return await TryReuseExistingPackageAsync(
                    manifest,
                    finalPath,
                    progress: null,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is InvalidDataException or
                  MediaDownloadException or
                  IOException or
                  UnauthorizedAccessException)
        {
            // A manifest that no longer validates, or a path that is not safe, means there
            // is nothing trustworthy to find.
            return null;
        }
    }

    private async Task<DownloadedUpdatePackage?>
        TryReuseExistingPackageAsync(
            UpdateManifest manifest,
            string finalPath,
            IProgress<UpdateDownloadProgress>? progress,
            CancellationToken cancellationToken)
    {
        try
        {
            FileInfo file =
                new(finalPath);

            if (!file.Exists ||
                file.Length != manifest.SizeBytes ||
                !UpdateManifestParser.TryParseSha256(
                    manifest.Sha256,
                    out byte[] expectedHash))
            {
                return null;
            }

            byte[] actualHash;

            await using (FileStream stream =
                new(
                    finalPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: CopyBufferSize,
                    useAsync: true))
            {
                actualHash =
                    await SHA256.HashDataAsync(
                            stream,
                            cancellationToken)
                        .ConfigureAwait(false);
            }

            if (!CryptographicOperations.FixedTimeEquals(
                    expectedHash,
                    actualHash))
            {
                return null;
            }

            progress?.Report(
                new UpdateDownloadProgress
                {
                    BytesReceived = manifest.SizeBytes,
                    TotalBytes = manifest.SizeBytes
                });

            return new DownloadedUpdatePackage
            {
                Manifest = manifest,
                FilePath = finalPath,
                SizeBytes = manifest.SizeBytes,
                Sha256 =
                    Convert.ToHexString(
                            actualHash)
                        .ToLowerInvariant(),
                DownloadedAtUtc = _clock.UtcNow
            };
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
            // An unreadable or locked file is simply downloaded again.
            return null;
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

    public Task PruneAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            currentVersion);

        cancellationToken.ThrowIfCancellationRequested();

        if (!TryParseNumericVersion(
                currentVersion,
                out Version? installed))
        {
            return Task.CompletedTask;
        }

        string ownedRoot =
            Path.GetFullPath(
                _paths.UpdatesDirectory);

        if (!Directory.Exists(
                ownedRoot))
        {
            return Task.CompletedTask;
        }

        _pathGuard.EnsureSafeDirectory(
            ownedRoot,
            ownedRoot);

        DateTime staleBeforeUtc =
            (_clock.UtcNow - StaleTemporaryFileAge)
            .UtcDateTime;

        foreach (string path
                 in Directory.EnumerateFiles(
                     ownedRoot,
                     "*",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if ((File.GetAttributes(
                         path) &
                     FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                string name =
                    Path.GetFileName(
                        path);

                bool remove;

                Match match =
                    InstallerNamePattern.Match(
                        name);

                if (match.Success)
                {
                    // Only packages for the running version or older are removed.
                    // A newer package is a pending update and must stay.
                    remove =
                        TryParseInstallerVersion(
                            match,
                            out Version? packageVersion) &&
                        packageVersion <= installed;
                }
                else
                {
                    remove =
                        name.StartsWith(
                            '.') &&
                        name.EndsWith(
                            ".tmp",
                            StringComparison.OrdinalIgnoreCase) &&
                        File.GetLastWriteTimeUtc(
                            path) <= staleBeforeUtc;
                }

                if (remove)
                {
                    string safePath =
                        _pathGuard.EnsureSafeFilePath(
                            ownedRoot,
                            path);

                    _pathGuard.DeleteOwnedFileIfPresent(
                        ownedRoot,
                        safePath);
                }
            }
            catch (Exception exception)
                when (exception is IOException or
                      UnauthorizedAccessException or
                      MediaDownloadException)
            {
                // A locked or protected file is left for a later pass.
            }
        }

        return Task.CompletedTask;
    }

    private static bool TryParseInstallerVersion(
        Match match,
        out Version? version)
    {
        version = null;

        if (!int.TryParse(
                match.Groups["major"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int major) ||
            !int.TryParse(
                match.Groups["minor"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int minor) ||
            !int.TryParse(
                match.Groups["patch"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int patch))
        {
            return false;
        }

        version =
            new Version(
                major,
                minor,
                patch);

        return true;
    }

    private static bool TryParseNumericVersion(
        string value,
        out Version? version)
    {
        // Prerelease and build suffixes are ignored: only the numeric core is compared.
        string core =
            value.Trim()
                .Split(
                    ['-', '+'],
                    2)[0];

        string[] parts =
            core.Split(
                '.');

        version = null;

        if (parts.Length != 3 ||
            !int.TryParse(
                parts[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int major) ||
            !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int minor) ||
            !int.TryParse(
                parts[2],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int patch))
        {
            return false;
        }

        version =
            new Version(
                major,
                minor,
                patch);

        return true;
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
