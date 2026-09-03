using CopyGIF.Core.Models;

namespace CopyGIF.Application.Search;

public interface IGifSearchCoordinator
{
    Task<GifSearchPage> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<GifSearchPage> SearchDebouncedAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        return SearchAsync(
            query,
            cancellationToken);
    }

    Task<GifSearchPage> TrendingAsync(
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "This search coordinator does not support Trending.");
    }

    Task<GifSearchPage> LoadMoreAsync(
        string query,
        string continuationToken,
        CancellationToken cancellationToken = default);

    Task<GifSearchPage> LoadMoreTrendingAsync(
        string continuationToken,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "This search coordinator does not support Trending pagination.");
    }
}
