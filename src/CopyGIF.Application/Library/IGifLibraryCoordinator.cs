using CopyGIF.Core.Models;

namespace CopyGIF.Application.Library;

public interface IGifLibraryCoordinator
{
    Task<LibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default);

    Task<LibrarySnapshot> AddFavoriteAsync(
        GifItem item,
        CancellationToken cancellationToken = default);

    Task<LibrarySnapshot> RemoveFavoriteAsync(
        GifIdentity identity,
        CancellationToken cancellationToken = default);

    Task<LibrarySnapshot> ClearFavoritesAsync(
        CancellationToken cancellationToken = default);

    Task<LibrarySnapshot> RecordRecentAsync(
        GifItem item,
        DownloadedGif copiedGif,
        CancellationToken cancellationToken = default);

    Task<LibrarySnapshot> ClearRecentsAsync(
        CancellationToken cancellationToken = default);
}
