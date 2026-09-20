using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IMigrationCoordinator
{
    Task<MigrationResult> MigrateIfNeededAsync(
        CancellationToken cancellationToken = default);
}
