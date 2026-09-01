using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IMigrationStateStore
{
    Task<MigrationState> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        MigrationState state,
        CancellationToken cancellationToken = default);
}
