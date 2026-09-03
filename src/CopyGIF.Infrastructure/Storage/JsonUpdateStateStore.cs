using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;

namespace CopyGIF.Infrastructure.Storage;

public sealed class JsonUpdateStateStore :
    IUpdateStateStore
{
    private const int MaximumVersionLength = 128;

    private readonly IApplicationPaths _paths;

    private readonly VersionedJsonSerializer
        _serializer;

    public JsonUpdateStateStore(
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

    public Task<UpdateState> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectoriesExist();

        return _serializer.LoadAsync(
            CreateDefinition(),
            cancellationToken);
    }

    public Task SaveAsync(
        UpdateState state,
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

    private VersionedJsonStoreDefinition<UpdateState>
        CreateDefinition()
    {
        return new VersionedJsonStoreDefinition<UpdateState>
        {
            PrimaryPath =
                _paths.UpdateStatePath,

            BackupPath =
                _paths.UpdateStateBackupPath,

            Description =
                "update state",

            MaximumBytes =
                StoragePolicy.MaximumUpdateStateFileBytes,

            CurrentSchemaVersion =
                UpdateState.CurrentSchemaVersion,

            CreateDefaults =
                static () => new UpdateState(),

            IsValid = IsValidState
        };
    }

    private static bool IsValidState(
        UpdateState state)
    {
        return state.SchemaVersion ==
                   UpdateState.CurrentSchemaVersion &&
               IsUtcOrNull(
                   state.LastCheckedAtUtc) &&
               IsValidOptionalVersion(
                   state.LastAvailableVersion) &&
               IsValidOptionalVersion(
                   state.LastDownloadedVersion) &&
               IsUtcOrNull(
                   state.LastDownloadedAtUtc) &&
               (state.LastDownloadedVersion is null) ==
               (state.LastDownloadedAtUtc is null);
    }

    private static bool IsUtcOrNull(
        DateTimeOffset? value)
    {
        return value is null ||
               value.Value.Offset ==
               TimeSpan.Zero;
    }

    private static bool IsValidOptionalVersion(
        string? value)
    {
        return value is null ||
               !string.IsNullOrWhiteSpace(
                   value) &&
               value.Length <=
               MaximumVersionLength &&
               !value.Any(
                   char.IsControl);
    }
}
