using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Core.Policies;
using CopyGIF.Application.Settings;

namespace CopyGIF.Application.Media;

public sealed class PreviewCoordinator :
    IPreviewCoordinator
{
    private readonly ISettingsStore _settingsStore;

    private readonly IPreviewCache _previewCache;
    private readonly EffectiveSettings? _effective;

    public PreviewCoordinator(
        ISettingsStore settingsStore,
        IPreviewCache previewCache,
        EffectiveSettings? effective = null)
    {
        _effective = effective;
        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        _previewCache =
            previewCache ??
            throw new ArgumentNullException(
                nameof(previewCache));
    }

    public async Task<Uri> GetThumbnailSourceAsync(GifItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (ProviderMediaPolicy.UsesDirectPreview(item.ProviderId))
        {
            if (!ProviderMediaPolicy.IsDirectMediaUri(item.ThumbnailUri)) throw new InvalidDataException("Invalid GIPHY media host.");
            return item.ThumbnailUri;
        }
        try
        {
            var source = await ResolveSourceAsync(item.ThumbnailUri, PreviewCacheKind.Thumbnail, cancellationToken);
            RepairDiagnostics.Record("thumbnail-cache", item.ProviderId, "ready");
            return source;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RepairDiagnostics.Record("thumbnail-cache", item.ProviderId, exception.GetType().Name);
            // A validated GIF can supply its first frame without starting animation.
            return await ResolveSourceAsync(item.PreviewUri ?? item.GifUri, PreviewCacheKind.Preview, cancellationToken);
        }
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

        if (_effective is not null) settings = await _effective.LoadAsync(cancellationToken).ConfigureAwait(false);

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

        if (ProviderMediaPolicy.UsesDirectPreview(item.ProviderId))
        {
            if (!ProviderMediaPolicy.IsDirectMediaUri(sourceUri)) throw new InvalidDataException("Invalid GIPHY media host.");
            return sourceUri;
        }
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

        if (ProviderMediaPolicy.UsesDirectPreview(item.ProviderId)) return;

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

        if (cachedEntry is null)
        {
            throw new InvalidDataException(
                "The preview is unavailable in the validated local cache.");
        }

        return CreateFileUri(cachedEntry.FilePath);
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
