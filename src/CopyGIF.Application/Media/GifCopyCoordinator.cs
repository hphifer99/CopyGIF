using CopyGIF.Application.Library;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Core.Policies;

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
        => await CopyAsync(item, searchQuery, onClipboardReady: null, cancellationToken).ConfigureAwait(false);

    public async Task<DownloadedGif> CopyAsync(
        GifItem item, string? searchQuery, Func<Task>? onClipboardReady,
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

        const GifDownloadPurpose purpose = GifDownloadPurpose.Clipboard;

        RepairDiagnostics.Record("copy-download", item.ProviderId, "started");
        DownloadedGif downloadedGif =
            await _gifDownloader
                .DownloadAsync(
                    item with { GifUri = item.Renditions.Select(settings.Library.GifQuality, item.GifUri) },
                    purpose,
                    cancellationToken)
                .ConfigureAwait(false);

        RepairDiagnostics.Record("copy-download", item.ProviderId, "ready", downloadedGif.SizeBytes);
        await _clipboardService
            .CopyGifAsync(
                downloadedGif,
                cancellationToken)
            .ConfigureAwait(false);

        RepairDiagnostics.Record("clipboard-handoff", item.ProviderId, "copied", downloadedGif.SizeBytes);
        if (onClipboardReady is not null) await onClipboardReady().ConfigureAwait(false);
        if (_gifDownloader is IReusableGifDownloader cleaner)
        {
            try { await cleaner.CleanupClipboardAsync(downloadedGif.FilePath, CancellationToken.None)
                .ConfigureAwait(false); }
            catch (Exception exception)
            { RepairDiagnostics.Record("clipboard-cleanup", item.ProviderId, exception.GetType().Name); }
        }
        if (ProviderMediaPolicy.AllowsPersistentLibrary(item.ProviderId))
        {
            try
            {
                DownloadedGif recent = settings.Library.StoreRecentsLocally &&
                    _gifDownloader is not IReusableGifDownloader
                    ? await _gifDownloader.DownloadAsync(item with { GifUri = item.Renditions.Select(settings.Library.SaveQuality, item.GifUri) }, GifDownloadPurpose.Recent, CancellationToken.None).ConfigureAwait(false)
                    : downloadedGif;
                await _libraryCoordinator.RecordRecentAsync(item, recent, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Clipboard success is not reversed by an unrelated Recents failure.
                RepairDiagnostics.Record("record-recent", item.ProviderId, exception.GetType().Name);
            }
        }

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
        catch (Exception exception)
        {
            RepairDiagnostics.Record("register-share", item.ProviderId, exception.GetType().Name);
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
