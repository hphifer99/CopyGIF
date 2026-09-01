using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IGifDownloader
{
    Task<DownloadedGif> DownloadAsync(
        GifItem item,
        GifDownloadPurpose purpose,
        CancellationToken cancellationToken = default);
}
