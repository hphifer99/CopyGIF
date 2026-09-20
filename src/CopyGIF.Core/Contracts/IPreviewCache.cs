using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IPreviewCache
{
    Task<PreviewCacheEntry?> TryGetAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        CancellationToken cancellationToken = default);

    Task<PreviewCacheEntry> StoreAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        Stream content,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        CancellationToken cancellationToken = default);

    Task CleanupAsync(
        CancellationToken cancellationToken = default);
}
