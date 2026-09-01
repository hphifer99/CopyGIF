using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IClipboardService
{
    Task CopyGifAsync(
        DownloadedGif gif,
        CancellationToken cancellationToken = default);
}
