namespace CopyGIF.Core.Contracts;

public interface IApplicationPaths
{
    string RootDirectory { get; }

    string SettingsPath { get; }

    string SettingsBackupPath { get; }

    string SecretsDirectory { get; }

    string CacheDirectory { get; }

    string FavoritesDirectory { get; }

    string RecentsDirectory { get; }

    void EnsureDirectoriesExist();
}