using CopyGIF.Core.Models;

namespace CopyGIF.Application.Media;

public interface IGifCopyCoordinator
{
    Task<DownloadedGif> CopyAsync(
        GifItem item,
        string? searchQuery,
        CancellationToken cancellationToken = default);
}
