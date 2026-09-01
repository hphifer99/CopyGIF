using CopyGIF.Core.Contracts;
using CopyGIF.Core.Policies;

namespace CopyGIF.Infrastructure.Storage;

public sealed class ApplicationPaths :
    IApplicationPaths
{
    public ApplicationPaths()
        : this(
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .LocalApplicationData),
                StoragePolicy.LibraryRootDirectoryName))
    {
    }

    public ApplicationPaths(
        string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            rootDirectory);

        RootDirectory =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    rootDirectory));
    }

    public string RootDirectory { get; }

    public string SettingsPath =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.SettingsFileName);

    public string SettingsBackupPath =>
        SettingsPath + ".bak";

    public string LibraryPath =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.LibraryFileName);

    public string LibraryBackupPath =>
        LibraryPath + ".bak";

    public string SearchHistoryPath =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.SearchHistoryFileName);

    public string SearchHistoryBackupPath =>
        SearchHistoryPath + ".bak";

    public string UpdateStatePath =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.UpdateStateFileName);

    public string UpdateStateBackupPath =>
        UpdateStatePath + ".bak";

    public string MigrationStatePath =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.MigrationStateFileName);

    public string MigrationStateBackupPath =>
        MigrationStatePath + ".bak";

    public string SecretsDirectory =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.SecretsDirectoryName);

    public string CacheDirectory =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.CacheDirectoryName);

    public string ThumbnailCacheDirectory =>
        Path.Combine(
            CacheDirectory,
            StoragePolicy.ThumbnailCacheDirectoryName);

    public string PreviewCacheDirectory =>
        Path.Combine(
            CacheDirectory,
            StoragePolicy.PreviewCacheDirectoryName);

    public string ClipboardCacheDirectory =>
        Path.Combine(
            CacheDirectory,
            StoragePolicy.ClipboardCacheDirectoryName);

    public string UpdatesDirectory =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.UpdatesDirectoryName);

    public string LogsDirectory =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.LogsDirectoryName);

    public string MigrationDirectory =>
        Path.Combine(
            RootDirectory,
            StoragePolicy.MigrationDirectoryName);

    public string FavoritesDirectory =>
        GetFavoritesDirectory(
            customStorageRoot: null);

    public string RecentsDirectory =>
        GetRecentsDirectory(
            customStorageRoot: null);

    public string GetLibraryRoot(
        string? customStorageRoot)
    {
        if (customStorageRoot is null)
        {
            return RootDirectory;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            customStorageRoot);

        string selectedRoot =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    customStorageRoot));

        return Path.Combine(
            selectedRoot,
            StoragePolicy.LibraryRootDirectoryName);
    }

    public string GetFavoritesDirectory(
        string? customStorageRoot)
    {
        return Path.Combine(
            GetLibraryRoot(
                customStorageRoot),
            StoragePolicy.FavoritesDirectoryName);
    }

    public string GetRecentsDirectory(
        string? customStorageRoot)
    {
        return Path.Combine(
            GetLibraryRoot(
                customStorageRoot),
            StoragePolicy.RecentsDirectoryName);
    }

    public void EnsureDirectoriesExist()
    {
        CreateDirectory(
            RootDirectory);

        CreateDirectory(
            SecretsDirectory);

        CreateDirectory(
            CacheDirectory);

        CreateDirectory(
            ThumbnailCacheDirectory);

        CreateDirectory(
            PreviewCacheDirectory);

        CreateDirectory(
            ClipboardCacheDirectory);

        CreateDirectory(
            UpdatesDirectory);

        CreateDirectory(
            LogsDirectory);

        CreateDirectory(
            MigrationDirectory);

        EnsureLibraryDirectoriesExist(
            customStorageRoot: null);
    }

    public void EnsureLibraryDirectoriesExist(
        string? customStorageRoot)
    {
        CreateDirectory(
            GetLibraryRoot(
                customStorageRoot));

        CreateDirectory(
            GetFavoritesDirectory(
                customStorageRoot));

        CreateDirectory(
            GetRecentsDirectory(
                customStorageRoot));
    }

    private static void CreateDirectory(
        string path)
    {
        Directory.CreateDirectory(
            path);
    }
}
