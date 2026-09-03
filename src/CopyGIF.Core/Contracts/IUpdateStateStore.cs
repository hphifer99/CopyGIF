using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IUpdateStateStore
{
    Task<UpdateState> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        UpdateState state,
        CancellationToken cancellationToken = default);
}
