using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Library;

public sealed class GifLibraryCoordinator :
    IGifLibraryCoordinator,
    IDisposable
{
    private readonly ILibraryStore _libraryStore;

    private readonly ISettingsStore _settingsStore;

    private readonly IGifDownloader _gifDownloader;

    private readonly ILibraryStorageMover _storageMover;

    private readonly IApplicationPaths _paths;

    private readonly IClock _clock;

    private readonly SemaphoreSlim _gate =
        new(
            initialCount: 1,
            maxCount: 1);

    private readonly StringComparer _pathComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private bool _disposed;

    public GifLibraryCoordinator(
        ILibraryStore libraryStore,
        ISettingsStore settingsStore,
        IGifDownloader gifDownloader,
        ILibraryStorageMover storageMover,
        IApplicationPaths paths,
        IClock clock)
    {
        _libraryStore =
            libraryStore ??
            throw new ArgumentNullException(
                nameof(libraryStore));

        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        _gifDownloader =
            gifDownloader ??
            throw new ArgumentNullException(
                nameof(gifDownloader));

        _storageMover =
            storageMover ??
            throw new ArgumentNullException(
                nameof(storageMover));

        _paths =
            paths ??
            throw new ArgumentNullException(
                nameof(paths));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));
    }

    public async Task<LibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return await _libraryStore
                .LoadAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LibrarySnapshot> AddFavoriteAsync(
        GifItem item,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            item);

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            AppSettings settings =
                await LoadSettingsAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            LibrarySnapshot current =
                await _libraryStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            if (current.Favorites.Any(
                    entry =>
                        HasIdentity(
                            entry,
                            item.StableIdentity)))
            {
                return current;
            }

            if (current.Favorites.Count >=
                settings.Library.FavoriteLimit)
            {
                throw new InvalidOperationException(
                    $"The favorite limit of {settings.Library.FavoriteLimit} has been reached.");
            }

            DownloadedGif? downloadedGif =
                null;

            if (settings.Library.StoreFavoritesLocally)
            {
                downloadedGif =
                    await _gifDownloader
                        .DownloadAsync(
                            item,
                            GifDownloadPurpose.Favorite,
                            cancellationToken)
                        .ConfigureAwait(false);
            }

            LibraryEntry favorite =
                CreateEntry(
                    item,
                    downloadedGif,
                    _clock.UtcNow,
                    lastCopiedAtUtc: null,
                    copyCount: 0);

            LibrarySnapshot updated =
                current with
                {
                    SchemaVersion =
                        LibrarySnapshot.CurrentSchemaVersion,

                    Favorites =
                        current.Favorites
                            .Prepend(
                                favorite)
                            .ToArray()
                };

            try
            {
                await _libraryStore
                    .SaveAsync(
                        updated,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                if (downloadedGif is not null)
                {
                    await TryDeletePathsAsync(
                            settings,
                            [downloadedGif.FilePath])
                        .ConfigureAwait(false);
                }

                throw;
            }

            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LibrarySnapshot> RemoveFavoriteAsync(
        GifIdentity identity,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            AppSettings settings =
                await LoadSettingsAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            LibrarySnapshot current =
                await _libraryStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            LibraryEntry[] removed =
                current.Favorites
                    .Where(
                        entry =>
                            HasIdentity(
                                entry,
                                identity))
                    .ToArray();

            if (removed.Length == 0)
            {
                return current;
            }

            LibrarySnapshot updated =
                current with
                {
                    SchemaVersion =
                        LibrarySnapshot.CurrentSchemaVersion,

                    Favorites =
                        current.Favorites
                            .Where(
                                entry =>
                                    !HasIdentity(
                                        entry,
                                        identity))
                            .ToArray()
                };

            await _libraryStore
                .SaveAsync(
                    updated,
                    cancellationToken)
                .ConfigureAwait(false);

            await TryDeletePathsAsync(
                    settings,
                    GetLocalPaths(
                        removed))
                .ConfigureAwait(false);

            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LibrarySnapshot> ClearFavoritesAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            AppSettings settings =
                await LoadSettingsAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            LibrarySnapshot current =
                await _libraryStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            if (current.Favorites.Count == 0)
            {
                return current;
            }

            LibrarySnapshot updated =
                current with
                {
                    SchemaVersion =
                        LibrarySnapshot.CurrentSchemaVersion,

                    Favorites = []
                };

            await _libraryStore
                .SaveAsync(
                    updated,
                    cancellationToken)
                .ConfigureAwait(false);

            await TryDeletePathsAsync(
                    settings,
                    GetLocalPaths(
                        current.Favorites))
                .ConfigureAwait(false);

            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LibrarySnapshot> RecordRecentAsync(
        GifItem item,
        DownloadedGif copiedGif,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentNullException.ThrowIfNull(
            item);

        ArgumentNullException.ThrowIfNull(
            copiedGif);

        if (!copiedGif.Identity.Equals(
                item.StableIdentity))
        {
            throw new InvalidDataException(
                "The copied GIF identity does not match the selected GIF.");
        }

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            AppSettings settings =
                await LoadSettingsAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            if (settings.Library.StoreRecentsLocally &&
                copiedGif.Purpose != GifDownloadPurpose.Recent)
            {
                throw new InvalidDataException(
                    "A locally retained Recent must use a Recent download.");
            }

            LibrarySnapshot current =
                await _libraryStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            LibraryEntry? existing =
                current.Recents
                    .FirstOrDefault(
                        entry =>
                            HasIdentity(
                                entry,
                                item.StableIdentity));

            int copyCount =
                existing is null
                    ? 1
                    : IncrementSaturating(
                        existing.CopyCount);

            DateTimeOffset copiedAtUtc =
                _clock.UtcNow;

            DownloadedGif? retainedGif =
                settings.Library.StoreRecentsLocally
                    ? copiedGif
                    : null;

            LibraryEntry recent =
                CreateEntry(
                    item,
                    retainedGif,
                    existing?.AddedAtUtc ??
                        copiedAtUtc,
                    copiedAtUtc,
                    copyCount);

            LibraryEntry[] ordered =
                current.Recents
                    .Where(
                        entry =>
                            !HasIdentity(
                                entry,
                                item.StableIdentity))
                    .Prepend(
                        recent)
                    .ToArray();

            LibraryEntry[] retained =
                ordered
                    .Take(
                        settings.Library.RecentLimit)
                    .ToArray();

            LibraryEntry[] evicted =
                ordered
                    .Skip(
                        settings.Library.RecentLimit)
                    .ToArray();

            LibrarySnapshot updated =
                current with
                {
                    SchemaVersion =
                        LibrarySnapshot.CurrentSchemaVersion,

                    Recents = retained
                };

            await _libraryStore
                .SaveAsync(
                    updated,
                    cancellationToken)
                .ConfigureAwait(false);

            List<string> cleanupPaths =
                GetLocalPaths(
                        evicted)
                    .ToList();

            if (existing?.LocalFilePath is not null &&
                !_pathComparer.Equals(
                    existing.LocalFilePath,
                    recent.LocalFilePath))
            {
                cleanupPaths.Add(
                    existing.LocalFilePath);
            }

            if (!settings.Library.StoreRecentsLocally &&
                copiedGif.Purpose == GifDownloadPurpose.Recent)
            {
                cleanupPaths.Add(
                    copiedGif.FilePath);
            }

            await TryDeletePathsAsync(
                    settings,
                    cleanupPaths)
                .ConfigureAwait(false);

            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LibrarySnapshot> ClearRecentsAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            AppSettings settings =
                await LoadSettingsAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            LibrarySnapshot current =
                await _libraryStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            if (current.Recents.Count == 0)
            {
                return current;
            }

            LibrarySnapshot updated =
                current with
                {
                    SchemaVersion =
                        LibrarySnapshot.CurrentSchemaVersion,

                    Recents = []
                };

            await _libraryStore
                .SaveAsync(
                    updated,
                    cancellationToken)
                .ConfigureAwait(false);

            await TryDeletePathsAsync(
                    settings,
                    GetLocalPaths(
                        current.Recents))
                .ConfigureAwait(false);

            return updated;
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

        _disposed = true;
        _gate.Dispose();
    }

    private async Task<AppSettings> LoadSettingsAsync(
        CancellationToken cancellationToken)
    {
        return AppSettingsNormalizer.Normalize(
            await _settingsStore
                .LoadAsync(
                    cancellationToken)
                .ConfigureAwait(false));
    }

    private async Task TryDeletePathsAsync(
        AppSettings settings,
        IEnumerable<string> filePaths)
    {
        string[] distinctPaths =
            filePaths
                .Where(
                    path =>
                        !string.IsNullOrWhiteSpace(
                            path))
                .Distinct(
                    _pathComparer)
                .ToArray();

        if (distinctPaths.Length == 0)
        {
            return;
        }

        string ownedRoot =
            _paths.GetLibraryRoot(
                settings.Library.CustomStorageRoot);

        try
        {
            await _storageMover
                .DeleteAsync(
                    ownedRoot,
                    distinctPaths,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
            when (exception is IOException or
                  UnauthorizedAccessException or
                  MediaDownloadException)
        {
        }
    }

    private static IEnumerable<string> GetLocalPaths(
        IEnumerable<LibraryEntry> entries)
    {
        return entries
            .Select(
                entry => entry.LocalFilePath)
            .OfType<string>();
    }

    private static LibraryEntry CreateEntry(
        GifItem item,
        DownloadedGif? downloadedGif,
        DateTimeOffset addedAtUtc,
        DateTimeOffset? lastCopiedAtUtc,
        int copyCount)
    {
        return new LibraryEntry
        {
            Identity = item.StableIdentity,
            Title = item.Title,
            Description = item.Description,
            GifUri = item.GifUri,
            ThumbnailUri = item.ThumbnailUri,
            PreviewUri = item.PreviewUri,
            SourcePageUri = item.SourcePageUri,
            LocalFilePath =
                downloadedGif?.FilePath,
            Width = item.Width,
            Height = item.Height,
            SizeBytes =
                downloadedGif?.SizeBytes ??
                item.SizeBytes,
            AddedAtUtc = addedAtUtc,
            LastCopiedAtUtc = lastCopiedAtUtc,
            CopyCount = copyCount
        };
    }

    private static bool HasIdentity(
        LibraryEntry entry,
        GifIdentity identity)
    {
        return string.Equals(
                   entry.Identity.ProviderId,
                   identity.ProviderId,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   entry.Identity.Id,
                   identity.Id,
                   StringComparison.Ordinal);
    }

    private static int IncrementSaturating(
        int value)
    {
        return value == int.MaxValue
            ? int.MaxValue
            : value + 1;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}
