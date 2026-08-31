using CopyGIF.Core.Models;

namespace CopyGIF.Application.Search;

public interface IGifSearchCoordinator
{
    Task<GifSearchPage> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<GifSearchPage> LoadMoreAsync(
        string query,
        string continuationToken,
        CancellationToken cancellationToken = default);
}