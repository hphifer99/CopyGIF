namespace CopyGIF.Core.Policies;

public static class StoragePolicy
{
    public const string SettingsFileName =
        "settings.json";

    public const string LibraryFileName =
        "library.json";

    public const string SearchHistoryFileName =
        "search-history.json";

    public const string UpdateStateFileName =
        "update-state.json";

    public const string MigrationStateFileName =
        "migration-state.json";

    public const string SecretsDirectoryName =
        "Secrets";

    public const string CacheDirectoryName =
        "Cache";

    public const string ThumbnailCacheDirectoryName =
        "Thumbnails";

    public const string PreviewCacheDirectoryName =
        "Previews";

    public const string ClipboardCacheDirectoryName =
        "Clipboard";

    public const string UpdatesDirectoryName =
        "Updates";

    public const string LogsDirectoryName =
        "Logs";

    public const string MigrationDirectoryName =
        "Migration";

    public const string FavoritesDirectoryName =
        "Favorites";

    public const string RecentsDirectoryName =
        "Recents";

    public const string LibraryRootDirectoryName =
        "CopyGIF";

    public const long MaximumSettingsFileBytes =
        1024L * 1024L;

    public const long MaximumLibraryFileBytes =
        16L * 1024L * 1024L;

    public const long MaximumSearchHistoryFileBytes =
        4L * 1024L * 1024L;

    public const long MaximumMigrationStateFileBytes =
        1024L * 1024L;

    public const long MaximumUpdateManifestBytes =
        256L * 1024L;

    public const int MaximumPreservedCorruptFiles = 5;
}
