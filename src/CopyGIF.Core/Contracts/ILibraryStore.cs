using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface ILibraryStore
{
    Task<LibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        LibrarySnapshot snapshot,
        CancellationToken cancellationToken = default);
}
