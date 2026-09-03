using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Media;

public sealed class PreviewCoordinator :
    IPreviewCoordinator
{
    private readonly ISettingsStore _settingsStore;

    private readonly IPreviewCache _previewCache;

    public PreviewCoordinator(
        ISettingsStore settingsStore,
        IPreviewCache previewCache)
    {
        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        _previewCache =
            previewCache ??
            throw new ArgumentNullException(
                nameof(previewCache));
    }

    public Task<Uri> GetThumbnailSourceAsync(
        GifItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            item);

        return ResolveSourceAsync(
            item.ThumbnailUri,
            PreviewCacheKind.Thumbnail,
            cancellationToken);
    }

    public async Task<Uri> GetAnimatedSourceAsync(
        GifItem item,
        bool reducedMotion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            item);

        AppSettings settings =
            AppSettingsNormalizer.Normalize(
                await _settingsStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false));

        if (reducedMotion ||
            !settings.Search.AnimatePreviews)
        {
            return await GetThumbnailSourceAsync(
                    item,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Uri sourceUri =
            item.PreviewUri ??
            item.GifUri;

        return await ResolveSourceAsync(
                sourceUri,
                PreviewCacheKind.Preview,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task InvalidateAsync(
        GifItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            item);

        await _previewCache
            .RemoveAsync(
                item.ThumbnailUri,
                PreviewCacheKind.Thumbnail,
                cancellationToken)
            .ConfigureAwait(false);

        await _previewCache
            .RemoveAsync(
                item.PreviewUri ??
                    item.GifUri,
                PreviewCacheKind.Preview,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task CleanupAsync(
        CancellationToken cancellationToken = default)
    {
        return _previewCache
            .CleanupAsync(
                cancellationToken);
    }

    private async Task<Uri> ResolveSourceAsync(
        Uri sourceUri,
        PreviewCacheKind kind,
        CancellationToken cancellationToken)
    {
        PreviewCacheEntry? cachedEntry =
            await _previewCache
                .TryGetAsync(
                    sourceUri,
                    kind,
                    cancellationToken)
                .ConfigureAwait(false);

        return cachedEntry is null
            ? sourceUri
            : CreateFileUri(
                cachedEntry.FilePath);
    }

    private static Uri CreateFileUri(
        string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        if (!Path.IsPathFullyQualified(
                filePath))
        {
            throw new InvalidDataException(
                "A cached preview path must be fully qualified.");
        }

        return new Uri(
            filePath,
            UriKind.Absolute);
    }
}
