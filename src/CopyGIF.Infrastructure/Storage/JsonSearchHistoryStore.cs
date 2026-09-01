using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;

namespace CopyGIF.Infrastructure.Storage;

public sealed class JsonSearchHistoryStore :
    ISearchHistoryStore
{
    private readonly IApplicationPaths _paths;
    private readonly VersionedJsonSerializer _serializer;

    public JsonSearchHistoryStore(
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

    public Task<SearchHistorySnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectoriesExist();

        return _serializer.LoadAsync(
            CreateDefinition(),
            cancellationToken);
    }

    public Task SaveAsync(
        SearchHistorySnapshot snapshot,
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

    public async Task ClearAsync(
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectoriesExist();

        await _serializer.SaveAsync(
            CreateDefinition(),
            new SearchHistorySnapshot(),
            cancellationToken);

        DeleteDataFile(
            _paths.SearchHistoryBackupPath);

        string directory =
            Path.GetDirectoryName(
                _paths.SearchHistoryPath)!;

        string fileName =
            Path.GetFileName(
                _paths.SearchHistoryPath);

        foreach (string corruptPath
                 in Directory.EnumerateFiles(
                     directory,
                     fileName + ".corrupt.*",
                     SearchOption.TopDirectoryOnly))
        {
            DeleteDataFile(
                corruptPath);
        }
    }

    private VersionedJsonStoreDefinition<SearchHistorySnapshot>
        CreateDefinition()
    {
        return new VersionedJsonStoreDefinition<SearchHistorySnapshot>
        {
            PrimaryPath = _paths.SearchHistoryPath,
            BackupPath = _paths.SearchHistoryBackupPath,
            Description = "search history",
            MaximumBytes =
                StoragePolicy.MaximumSearchHistoryFileBytes,
            CurrentSchemaVersion =
                SearchHistorySnapshot.CurrentSchemaVersion,
            CreateDefaults =
                static () => new SearchHistorySnapshot(),
            IsValid = IsValidSnapshot
        };
    }

    private static bool IsValidSnapshot(
        SearchHistorySnapshot snapshot)
    {
        return snapshot.SchemaVersion ==
                   SearchHistorySnapshot.CurrentSchemaVersion &&
               snapshot.Entries is not null &&
               snapshot.Entries.Count <= 500 &&
               snapshot.Entries.All(
                   static entry =>
                       entry is not null &&
                       !string.IsNullOrWhiteSpace(
                           entry.Query) &&
                       entry.Query.Length <= 500 &&
                       entry.UseCount >= 1);
    }

    private static void DeleteDataFile(
        string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        FileAttributes attributes =
            File.GetAttributes(path);

        if ((attributes &
             FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "A search-history reparse point was not deleted.");
        }

        File.Delete(path);
    }
}
