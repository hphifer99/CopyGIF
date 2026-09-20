using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;

namespace CopyGIF.Infrastructure.Storage;

public sealed class JsonMigrationStateStore :
    IMigrationStateStore
{
    private readonly IApplicationPaths _paths;
    private readonly VersionedJsonSerializer _serializer;

    public JsonMigrationStateStore(
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

    public Task<MigrationState> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectoriesExist();

        return _serializer.LoadAsync(
            CreateDefinition(),
            cancellationToken);
    }

    public Task SaveAsync(
        MigrationState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            state);

        _paths.EnsureDirectoriesExist();

        return _serializer.SaveAsync(
            CreateDefinition(),
            state,
            cancellationToken);
    }

    private VersionedJsonStoreDefinition<MigrationState>
        CreateDefinition()
    {
        return new VersionedJsonStoreDefinition<MigrationState>
        {
            PrimaryPath = _paths.MigrationStatePath,
            BackupPath = _paths.MigrationStateBackupPath,
            Description = "migration state",
            MaximumBytes =
                StoragePolicy.MaximumMigrationStateFileBytes,
            CurrentSchemaVersion =
                MigrationState.CurrentSchemaVersion,
            CreateDefaults =
                static () => new MigrationState(),
            IsValid =
                static state =>
                    state.SchemaVersion ==
                        MigrationState.CurrentSchemaVersion &&
                    state.IsCompleted ==
                        (state.CompletedAtUtc is not null)
        };
    }
}
