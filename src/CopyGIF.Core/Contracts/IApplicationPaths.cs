namespace CopyGIF.Core.Contracts;

public interface IApplicationPaths
{
    string RootDirectory { get; }

    string SettingsPath { get; }

    string SettingsBackupPath { get; }

    string LibraryPath { get; }

    string LibraryBackupPath { get; }

    string SearchHistoryPath { get; }

    string SearchHistoryBackupPath { get; }

    string UpdateStatePath { get; }

    string UpdateStateBackupPath { get; }

    string MigrationStatePath { get; }

    string MigrationStateBackupPath { get; }

    string SecretsDirectory { get; }

    string CacheDirectory { get; }

    string ThumbnailCacheDirectory { get; }

    string PreviewCacheDirectory { get; }

    string ClipboardCacheDirectory { get; }

    string UpdatesDirectory { get; }

    string LogsDirectory { get; }

    string MigrationDirectory { get; }

    string FavoritesDirectory { get; }

    string RecentsDirectory { get; }

    string GetLibraryRoot(
        string? customStorageRoot);

    string GetFavoritesDirectory(
        string? customStorageRoot);

    string GetRecentsDirectory(
        string? customStorageRoot);

    void EnsureDirectoriesExist();

    void EnsureLibraryDirectoriesExist(
        string? customStorageRoot);
}
