namespace CopyGIF.Infrastructure.Storage;

public sealed class ApplicationPaths
{
    private const string ApplicationDirectoryName = "CopyGIF";

    public ApplicationPaths()
        : this(
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                ApplicationDirectoryName))
    {
    }

    public ApplicationPaths(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public string SettingsPath =>
        Path.Combine(RootDirectory, "settings.json");

    public string SettingsBackupPath =>
        Path.Combine(RootDirectory, "settings.json.bak");

    public string SecretsDirectory =>
        Path.Combine(RootDirectory, "Secrets");

    public string CacheDirectory =>
        Path.Combine(RootDirectory, "Cache");

    public string FavoritesDirectory =>
        Path.Combine(RootDirectory, "Favorites");

    public string RecentsDirectory =>
        Path.Combine(RootDirectory, "Recents");

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(SecretsDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(FavoritesDirectory);
        Directory.CreateDirectory(RecentsDirectory);
    }
}