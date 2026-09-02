using System.Security.Cryptography;
using System.Text;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Infrastructure.Storage;

namespace CopyGIF.Infrastructure.Media;

public sealed class PreviewCache :
    IPreviewCache,
    IDisposable
{
    private static readonly byte[] PngSignature =
    [
        0x89,
        (byte)'P',
        (byte)'N',
        (byte)'G',
        0x0D,
        0x0A,
        0x1A,
        0x0A
    ];

    private const string CacheExtension =
        ".cache";

    private const string TemporaryExtension =
        ".tmp";

    private readonly IApplicationPaths
        _paths;

    private readonly IClock _clock;

    private readonly OwnedPathGuard
        _pathGuard;

    private readonly PreviewCacheLimits
        _limits;

    private readonly SemaphoreSlim _gate =
        new(1, 1);

    private bool _disposed;

    public PreviewCache(
        IApplicationPaths paths,
        IClock clock,
        OwnedPathGuard pathGuard,
        PreviewCacheLimits limits)
    {
        _paths =
            paths ??
            throw new ArgumentNullException(
                nameof(paths));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));

        _pathGuard =
            pathGuard ??
            throw new ArgumentNullException(
                nameof(pathGuard));

        _limits =
            limits ??
            throw new ArgumentNullException(
                nameof(limits));

        _limits.Validate();
    }

    public async Task<PreviewCacheEntry?> TryGetAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        CancellationToken cancellationToken = default)
    {
        ValidateSourceUri(
            sourceUri);

        ValidateKind(
            kind);

        ThrowIfDisposed();

        await _gate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            CacheLocation location =
                GetLocation(
                    sourceUri,
                    kind);

            _pathGuard.EnsureSafeDirectory(
                location.OwnedRoot,
                location.Directory);

            string filePath =
                _pathGuard.EnsureSafeFilePath(
                    location.OwnedRoot,
                    location.FilePath);

            if (!File.Exists(
                    filePath))
            {
                return null;
            }

            FileInfo file =
                new(
                    filePath);

            if (!IsUsableCacheFile(
                    file,
                    kind,
                    location.MaximumItemBytes) ||
                IsExpired(
                    file.LastWriteTimeUtc))
            {
                _pathGuard.DeleteOwnedFileIfPresent(
                    location.OwnedRoot,
                    filePath);

                return null;
            }

            DateTimeOffset accessedAtUtc =
                _clock.UtcNow;

            File.SetLastWriteTimeUtc(
                filePath,
                accessedAtUtc.UtcDateTime);

            return CreateEntry(
                sourceUri,
                kind,
                file,
                accessedAtUtc);
        }
        catch (MediaDownloadException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
            throw StorageFailure(
                "The preview cache could not be read safely.",
                exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<PreviewCacheEntry> StoreAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ValidateSourceUri(
            sourceUri);

        ValidateKind(
            kind);

        ArgumentNullException.ThrowIfNull(
            content);

        if (!content.CanRead)
        {
            throw new ArgumentException(
                "The cache content stream must be readable.",
                nameof(content));
        }

        ThrowIfDisposed();

        await _gate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        CacheLocation location =
            GetLocation(
                sourceUri,
                kind);

        string? temporaryPath = null;

        try
        {
            _pathGuard.EnsureSafeDirectory(
                location.OwnedRoot,
                location.Directory);

            string finalPath =
                _pathGuard.EnsureSafeFilePath(
                    location.OwnedRoot,
                    location.FilePath);

            temporaryPath =
                _pathGuard.EnsureSafeFilePath(
                    location.OwnedRoot,
                    Path.Combine(
                        location.Directory,
                        $".{Path.GetFileName(finalPath)}." +
                        $"{Guid.NewGuid():N}" +
                        TemporaryExtension));

            long sizeBytes =
                await WriteBoundedAsync(
                        content,
                        temporaryPath,
                        kind,
                        location.MaximumItemBytes,
                        cancellationToken)
                    .ConfigureAwait(false);

            _pathGuard.EnsureSafeFilePath(
                location.OwnedRoot,
                finalPath);

            File.Move(
                temporaryPath,
                finalPath,
                overwrite: true);

            temporaryPath = null;

            DateTimeOffset storedAtUtc =
                _clock.UtcNow;

            File.SetCreationTimeUtc(
                finalPath,
                storedAtUtc.UtcDateTime);

            File.SetLastWriteTimeUtc(
                finalPath,
                storedAtUtc.UtcDateTime);

            await CleanupCoreAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            return new PreviewCacheEntry
            {
                SourceUri = sourceUri,
                Kind = kind,
                FilePath = finalPath,
                SizeBytes = sizeBytes,
                CreatedAtUtc = storedAtUtc,
                LastAccessedAtUtc = storedAtUtc
            };
        }
        catch (OperationCanceledException)
        {
            DeleteTemporaryFile(
                location.OwnedRoot,
                temporaryPath);

            throw;
        }
        catch (MediaDownloadException)
        {
            DeleteTemporaryFile(
                location.OwnedRoot,
                temporaryPath);

            throw;
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
            DeleteTemporaryFile(
                location.OwnedRoot,
                temporaryPath);

            throw StorageFailure(
                "The preview cache could not be written safely.",
                exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        CancellationToken cancellationToken = default)
    {
        ValidateSourceUri(
            sourceUri);

        ValidateKind(
            kind);

        ThrowIfDisposed();

        await _gate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            CacheLocation location =
                GetLocation(
                    sourceUri,
                    kind);

            _pathGuard.EnsureSafeDirectory(
                location.OwnedRoot,
                location.Directory);

            _pathGuard.DeleteOwnedFileIfPresent(
                location.OwnedRoot,
                location.FilePath);
        }
        catch (MediaDownloadException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
            throw StorageFailure(
                "The preview cache entry could not be removed safely.",
                exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CleanupAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await CleanupCoreAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (MediaDownloadException)
        {
            throw;
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException)
        {
            throw StorageFailure(
                "The preview cache could not be cleaned safely.",
                exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gate.Dispose();
        _disposed = true;
    }

    private async Task CleanupCoreAsync(
        CancellationToken cancellationToken)
    {
        await CleanupDirectoryAsync(
                _paths.ThumbnailCacheDirectory,
                _limits.MaximumThumbnailCacheBytes,
                cancellationToken)
            .ConfigureAwait(false);

        await CleanupDirectoryAsync(
                _paths.PreviewCacheDirectory,
                _limits.MaximumPreviewCacheBytes,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task CleanupDirectoryAsync(
        string directory,
        long maximumCacheBytes,
        CancellationToken cancellationToken)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        _pathGuard.EnsureSafeDirectory(
            _paths.CacheDirectory,
            directory);

        DeleteSafeTemporaryFiles(
            directory,
            cancellationToken);

        List<FileInfo> files =
            EnumerateSafeCacheFiles(
                    directory,
                    cancellationToken)
                .ToList();

        foreach (FileInfo file
                 in files.Where(
                     file =>
                         IsExpired(
                             file.LastWriteTimeUtc)))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            _pathGuard.DeleteOwnedFileIfPresent(
                _paths.CacheDirectory,
                file.FullName);
        }

        files =
            EnumerateSafeCacheFiles(
                    directory,
                    cancellationToken)
                .OrderBy(
                    file =>
                        file.LastWriteTimeUtc)
                .ThenBy(
                    file =>
                        file.Name,
                    StringComparer.Ordinal)
                .ToList();

        long totalBytes =
            files.Sum(
                file =>
                    file.Length);

        foreach (FileInfo file
                 in files)
        {
            if (totalBytes <=
                maximumCacheBytes)
            {
                break;
            }

            cancellationToken
                .ThrowIfCancellationRequested();

            _pathGuard.DeleteOwnedFileIfPresent(
                _paths.CacheDirectory,
                file.FullName);

            totalBytes -=
                file.Length;
        }

        return Task.CompletedTask;
    }

    private IEnumerable<FileInfo>
        EnumerateSafeCacheFiles(
            string directory,
            CancellationToken cancellationToken)
    {
        foreach (string path
                 in Directory.EnumerateFiles(
                     directory,
                     $"*{CacheExtension}",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            FileAttributes attributes =
                File.GetAttributes(
                    path);

            if ((attributes &
                 FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            string safePath =
                _pathGuard.EnsureSafeFilePath(
                    _paths.CacheDirectory,
                    path);

            yield return new FileInfo(
                safePath);
        }
    }

    private void DeleteSafeTemporaryFiles(
        string directory,
        CancellationToken cancellationToken)
    {
        foreach (string path
                 in Directory.EnumerateFiles(
                     directory,
                     $"*{TemporaryExtension}",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            FileAttributes attributes =
                File.GetAttributes(
                    path);

            if ((attributes &
                 FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            _pathGuard.DeleteOwnedFileIfPresent(
                _paths.CacheDirectory,
                path);
        }
    }

    private static async Task<long> WriteBoundedAsync(
        Stream source,
        string temporaryPath,
        PreviewCacheKind kind,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
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
                maximumBytes)
            {
                throw new MediaDownloadException(
                    MediaDownloadFailure.TooLarge,
                    "The preview cache item exceeds its size limit.");
            }

            int headerBytesToCopy =
                Math.Min(
                    read,
                    header.Length -
                    headerLength);

            if (headerBytesToCopy > 0)
            {
                buffer.AsSpan(
                        0,
                        headerBytesToCopy)
                    .CopyTo(
                        header.AsSpan(
                            headerLength));

                headerLength +=
                    headerBytesToCopy;
            }

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

        if (!HasValidContentHeader(
                kind,
                header,
                headerLength))
        {
            throw new MediaDownloadException(
                MediaDownloadFailure.InvalidGif,
                kind == PreviewCacheKind.Preview
                    ? "Animated preview content is not a valid GIF."
                    : "Thumbnail content is not a supported image.");
        }

        return totalBytes;
    }

    private static bool IsUsableCacheFile(
        FileInfo file,
        PreviewCacheKind kind,
        long maximumBytes)
    {
        if (file.Length <= 0 ||
            file.Length > maximumBytes)
        {
            return false;
        }

        byte[] header =
            new byte[13];

        int headerLength;

        using (FileStream stream =
               new(
                   file.FullName,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read,
                   bufferSize: 4096,
                   options:
                       FileOptions.SequentialScan))
        {
            headerLength =
                stream.Read(
                    header,
                    0,
                    header.Length);
        }

        return HasValidContentHeader(
            kind,
            header,
            headerLength);
    }

    private static bool HasValidContentHeader(
        PreviewCacheKind kind,
        byte[] header,
        int headerLength)
    {
        bool validGif =
            HasValidGifHeader(
                header,
                headerLength);

        if (kind ==
            PreviewCacheKind.Preview)
        {
            return validGif;
        }

        bool validJpeg =
            headerLength >= 3 &&
            header[0] == 0xFF &&
            header[1] == 0xD8 &&
            header[2] == 0xFF;

        bool validPng =
            headerLength >= 8 &&
            header.AsSpan(
                    0,
                    8)
                .SequenceEqual(
                    PngSignature);

        return validGif ||
               validJpeg ||
               validPng;
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

    private static PreviewCacheEntry CreateEntry(
        Uri sourceUri,
        PreviewCacheKind kind,
        FileInfo file,
        DateTimeOffset accessedAtUtc)
    {
        DateTime createdUtc =
            file.CreationTimeUtc;

        DateTimeOffset createdAtUtc =
            createdUtc == DateTime.MinValue
                ? accessedAtUtc
                : new DateTimeOffset(
                    DateTime.SpecifyKind(
                        createdUtc,
                        DateTimeKind.Utc));

        return new PreviewCacheEntry
        {
            SourceUri = sourceUri,
            Kind = kind,
            FilePath = file.FullName,
            SizeBytes = file.Length,
            CreatedAtUtc = createdAtUtc,
            LastAccessedAtUtc = accessedAtUtc
        };
    }

    private CacheLocation GetLocation(
        Uri sourceUri,
        PreviewCacheKind kind)
    {
        string directory =
            kind switch
            {
                PreviewCacheKind.Thumbnail =>
                    _paths.ThumbnailCacheDirectory,

                PreviewCacheKind.Preview =>
                    _paths.PreviewCacheDirectory,

                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(kind),
                        kind,
                        "The preview cache kind is not supported.")
            };

        long maximumItemBytes =
            kind == PreviewCacheKind.Thumbnail
                ? _limits.MaximumThumbnailBytes
                : _limits.MaximumPreviewBytes;

        string fileName =
            CreateStableFileName(
                sourceUri);

        return new CacheLocation(
            _paths.CacheDirectory,
            directory,
            Path.Combine(
                directory,
                fileName),
            maximumItemBytes);
    }

    private bool IsExpired(
        DateTime lastWriteTimeUtc)
    {
        DateTimeOffset lastAccessedAtUtc =
            new(
                DateTime.SpecifyKind(
                    lastWriteTimeUtc,
                    DateTimeKind.Utc));

        return lastAccessedAtUtc <=
               _clock.UtcNow -
               _limits.Retention;
    }

    private static string CreateStableFileName(
        Uri sourceUri)
    {
        byte[] uriBytes =
            Encoding.UTF8.GetBytes(
                sourceUri.AbsoluteUri);

        byte[] uriHash =
            SHA256.HashData(
                uriBytes);

        return Convert.ToHexString(
                uriHash)
            .ToLowerInvariant() +
            CacheExtension;
    }

    private static void ValidateSourceUri(
        Uri sourceUri)
    {
        ArgumentNullException.ThrowIfNull(
            sourceUri);

        if (!sourceUri.IsAbsoluteUri ||
            !string.Equals(
                sourceUri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Preview cache source URIs must use absolute HTTPS URLs.",
                nameof(sourceUri));
        }
    }

    private static void ValidateKind(
        PreviewCacheKind kind)
    {
        if (!Enum.IsDefined(
                kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "The preview cache kind is not supported.");
        }
    }

    private void DeleteTemporaryFile(
        string ownedRoot,
        string? temporaryPath)
    {
        if (temporaryPath is null)
        {
            return;
        }

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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }

    private static MediaDownloadException StorageFailure(
        string message,
        Exception exception)
    {
        return new MediaDownloadException(
            MediaDownloadFailure.Storage,
            message,
            exception);
    }

    private sealed record CacheLocation(
        string OwnedRoot,
        string Directory,
        string FilePath,
        long MaximumItemBytes);
}
