using CopyGIF.Application.Library;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Media;

public sealed class GifCopyCoordinator :
    IGifCopyCoordinator
{
    private readonly ISettingsStore _settingsStore;

    private readonly IGifDownloader _gifDownloader;

    private readonly IClipboardService _clipboardService;

    private readonly IGifLibraryCoordinator _libraryCoordinator;

    private readonly IProviderCatalog _providerCatalog;

    public GifCopyCoordinator(
        ISettingsStore settingsStore,
        IGifDownloader gifDownloader,
        IClipboardService clipboardService,
        IGifLibraryCoordinator libraryCoordinator,
        IProviderCatalog providerCatalog)
    {
        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        _gifDownloader =
            gifDownloader ??
            throw new ArgumentNullException(
                nameof(gifDownloader));

        _clipboardService =
            clipboardService ??
            throw new ArgumentNullException(
                nameof(clipboardService));

        _libraryCoordinator =
            libraryCoordinator ??
            throw new ArgumentNullException(
                nameof(libraryCoordinator));

        _providerCatalog =
            providerCatalog ??
            throw new ArgumentNullException(
                nameof(providerCatalog));
    }

    public async Task<DownloadedGif> CopyAsync(
        GifItem item,
        string? searchQuery,
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

        GifDownloadPurpose purpose =
            settings.Library.StoreRecentsLocally
                ? GifDownloadPurpose.Recent
                : GifDownloadPurpose.Clipboard;

        DownloadedGif downloadedGif =
            await _gifDownloader
                .DownloadAsync(
                    item,
                    purpose,
                    cancellationToken)
                .ConfigureAwait(false);

        await _clipboardService
            .CopyGifAsync(
                downloadedGif,
                cancellationToken)
            .ConfigureAwait(false);

        await _libraryCoordinator
            .RecordRecentAsync(
                item,
                downloadedGif,
                CancellationToken.None)
            .ConfigureAwait(false);

        await TryRegisterShareAsync(
                item,
                searchQuery,
                cancellationToken)
            .ConfigureAwait(false);

        return downloadedGif;
    }

    private async Task TryRegisterShareAsync(
        GifItem item,
        string? searchQuery,
        CancellationToken cancellationToken)
    {
        IGifProvider provider =
            _providerCatalog
                .GetRequiredProvider(
                    item.ProviderId);

        try
        {
            await provider
                .RegisterShareAsync(
                    item.Id,
                    NormalizeSearchQuery(
                        searchQuery),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (GifProviderException)
        {
        }
    }

    private static string? NormalizeSearchQuery(
        string? searchQuery)
    {
        return string.IsNullOrWhiteSpace(
                searchQuery)
            ? null
            : searchQuery.Trim();
    }
}
