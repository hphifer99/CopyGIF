using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IReusableGifDownloader : IGifDownloader
{
    Task<DownloadedGif> RetainAsync(
        GifItem item, DownloadedGif clipboardGif,
        GifDownloadPurpose purpose, CancellationToken cancellationToken = default);

    Task CleanupClipboardAsync(string activeClipboardFilePath,
        CancellationToken cancellationToken = default);
}
