namespace CopyGIF.Infrastructure.Storage;

public sealed class LegacySettingsDetectedException
    : Exception
{
    public LegacySettingsDetectedException(string path)
        : base(
            "Legacy CopyGIF settings were detected. " +
            "They must be migrated before V2 settings can be used.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        SettingsPath = path;
    }

    public string SettingsPath { get; }
}