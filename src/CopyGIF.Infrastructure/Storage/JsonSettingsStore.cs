using CopyGIF.Core.Contracts;
using CopyGIF.Core.Policies;
using CopyGIF.Core.Settings;

namespace CopyGIF.Infrastructure.Storage;

public sealed class JsonSettingsStore :
    ISettingsStore
{
    private readonly IApplicationPaths _paths;
    private readonly VersionedJsonSerializer _serializer;

    public JsonSettingsStore(
        IApplicationPaths paths)
        : this(
            paths,
            new VersionedJsonSerializer(
                new AtomicFileWriter(),
                new CorruptFileRecovery()))
    {
    }

    public JsonSettingsStore(
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

    public async Task<AppSettings> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectoriesExist();

        AppSettings settings =
            await _serializer.LoadAsync(
                CreateDefinition(),
                cancellationToken);

        return NormalizeAndValidate(
            settings);
    }

    public async Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        _paths.EnsureDirectoriesExist();

        AppSettings normalized =
            NormalizeAndValidate(
                settings);

        await _serializer.SaveAsync(
            CreateDefinition(),
            normalized,
            cancellationToken);
    }

    private VersionedJsonStoreDefinition<AppSettings>
        CreateDefinition()
    {
        return new VersionedJsonStoreDefinition<AppSettings>
        {
            PrimaryPath = _paths.SettingsPath,
            BackupPath = _paths.SettingsBackupPath,
            Description = "settings",
            MaximumBytes =
                StoragePolicy.MaximumSettingsFileBytes,
            CurrentSchemaVersion =
                AppSettings.CurrentSchemaVersion,
            CreateDefaults =
                static () => new AppSettings(),
            IsValid =
                static _ => true,
            MissingSchemaExceptionFactory =
                static path =>
                    new LegacySettingsDetectedException(
                        path)
        };
    }

    private static AppSettings NormalizeAndValidate(
        AppSettings settings)
    {
        AppSettings normalized =
            AppSettingsNormalizer.Normalize(
                settings);

        if (!AppSettingsValidator.IsValid(
                normalized))
        {
            throw new InvalidDataException(
                "CopyGIF settings could not be normalized into a valid state.");
        }

        return normalized;
    }
}
