using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;

namespace CopyGIF.Infrastructure.Storage;

public sealed class JsonLibraryStore :
    ILibraryStore
{
    private readonly IApplicationPaths _paths;
    private readonly VersionedJsonSerializer _serializer;

    public JsonLibraryStore(
        IApplicationPaths paths,
        VersionedJsonSerializer serializer)
    {
        _paths =
            paths ??
            throw new ArgumentNullException(
                nameof(paths));

        _serializer =
            serializer ??
            throw new ArgumentNullException(
                nameof(serializer));
    }

    public Task<LibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectoriesExist();

        return _serializer.LoadAsync(
            CreateDefinition(),
            cancellationToken);
    }

    public Task SaveAsync(
        LibrarySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        _paths.EnsureDirectoriesExist();

        return _serializer.SaveAsync(
            CreateDefinition(),
            snapshot,
            cancellationToken);
    }

    private VersionedJsonStoreDefinition<LibrarySnapshot>
        CreateDefinition()
    {
        return new VersionedJsonStoreDefinition<LibrarySnapshot>
        {
            PrimaryPath = _paths.LibraryPath,
            BackupPath = _paths.LibraryBackupPath,
            Description = "library",
            MaximumBytes =
                StoragePolicy.MaximumLibraryFileBytes,
            CurrentSchemaVersion =
                LibrarySnapshot.CurrentSchemaVersion,
            CreateDefaults =
                static () => new LibrarySnapshot(),
            IsValid = IsValidSnapshot
        };
    }

    private static bool IsValidSnapshot(
        LibrarySnapshot snapshot)
    {
        if (snapshot.SchemaVersion !=
                LibrarySnapshot.CurrentSchemaVersion ||
            snapshot.Favorites is null ||
            snapshot.Recents is null ||
            snapshot.Favorites.Count > 500 ||
            snapshot.Recents.Count > 100)
        {
            return false;
        }

        return snapshot.Favorites
            .Concat(snapshot.Recents)
            .All(IsValidEntry);
    }

    private static bool IsValidEntry(
        LibraryEntry? entry)
    {
        return entry is not null &&
               !string.IsNullOrWhiteSpace(
                   entry.Identity.ProviderId) &&
               !string.IsNullOrWhiteSpace(
                   entry.Identity.Id) &&
               IsHttps(entry.GifUri) &&
               IsHttps(entry.ThumbnailUri) &&
               (entry.PreviewUri is null ||
                IsHttps(entry.PreviewUri)) &&
               (entry.SourcePageUri is null ||
                IsHttps(entry.SourcePageUri)) &&
               entry.Width >= 0 &&
               entry.Height >= 0 &&
               entry.CopyCount >= 0 &&
               (entry.SizeBytes is null ||
                entry.SizeBytes >= 0 &&
                entry.SizeBytes <=
                    MediaPolicy.MaximumGifBytes);
    }

    private static bool IsHttps(
        Uri? uri)
    {
        return uri is not null &&
               uri.IsAbsoluteUri &&
               string.Equals(
                   uri.Scheme,
                   Uri.UriSchemeHttps,
                   StringComparison.OrdinalIgnoreCase);
    }
}
