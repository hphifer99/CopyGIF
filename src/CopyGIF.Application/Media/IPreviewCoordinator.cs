using CopyGIF.Core.Models;

namespace CopyGIF.Application.Media;

public interface IPreviewCoordinator
{
    Task<Uri> GetThumbnailSourceAsync(
        GifItem item,
        CancellationToken cancellationToken = default);

    Task<Uri> GetAnimatedSourceAsync(
        GifItem item,
        bool reducedMotion,
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(
        GifItem item,
        CancellationToken cancellationToken = default);

    Task CleanupAsync(
        CancellationToken cancellationToken = default);
}
