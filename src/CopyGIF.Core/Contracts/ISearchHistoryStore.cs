using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface ISearchHistoryStore
{
    Task<SearchHistorySnapshot> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        SearchHistorySnapshot snapshot,
        CancellationToken cancellationToken = default);

    Task ClearAsync(
        CancellationToken cancellationToken = default);
}
