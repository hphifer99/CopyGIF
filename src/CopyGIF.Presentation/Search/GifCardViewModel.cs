using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Library;
using CopyGIF.Application.Media;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;
using CopyGIF.Presentation.Common;

namespace CopyGIF.Presentation.Search;

public sealed class GifCardViewModel :
    ObservableObject
{
    private readonly IGifCopyCoordinator
        _copyCoordinator;

    private readonly IGifLibraryCoordinator
        _libraryCoordinator;

    private readonly IPreviewCoordinator
        _previewCoordinator;

    private readonly string?
        _searchQuery;

    private Uri? _thumbnailSource;

    private Uri? _currentSource;

    private bool _isFavorite;

    private bool _isPreviewing;

    private bool _isPreviewLoading;

    private bool _reducedMotion;

    private AsyncOperationState
        _operationState =
            AsyncOperationState.Idle;

    private UserMessage?
        _message;

    public GifCardViewModel(
        GifItem item,
        IGifCopyCoordinator copyCoordinator,
        IGifLibraryCoordinator libraryCoordinator,
        IPreviewCoordinator previewCoordinator,
        bool isFavorite = false,
        string? searchQuery = null,
        bool reducedMotion = false)
    {
        Item =
            item ??
            throw new ArgumentNullException(
                nameof(item));

        _copyCoordinator =
            copyCoordinator ??
            throw new ArgumentNullException(
                nameof(copyCoordinator));

        _libraryCoordinator =
            libraryCoordinator ??
            throw new ArgumentNullException(
                nameof(libraryCoordinator));

        _previewCoordinator =
            previewCoordinator ??
            throw new ArgumentNullException(
                nameof(previewCoordinator));

        _searchQuery =
            NormalizeSearchQuery(
                searchQuery);

        _isFavorite =
            isFavorite;

        _reducedMotion =
            reducedMotion;

        // KLIPY cards must wait for a validated local thumbnail before XAML
        // receives a source URI. GIPHY previews use approved direct media hosts.
        _thumbnailSource = ProviderMediaPolicy.UsesDirectPreview(item.ProviderId) &&
            ProviderMediaPolicy.IsDirectMediaUri(item.ThumbnailUri)
            ? item.ThumbnailUri : null;

        _currentSource = _thumbnailSource;

        LoadThumbnailCommand = new AsyncRelayCommand<bool?>(async (retry, token) =>
        {
            if (retry == true) { _hasThumbnail = false; await _previewCoordinator.InvalidateAsync(Item, token); }
            await LoadThumbnailAsync(token);
        });

        CopyCommand =
            new AsyncRelayCommand(
                CopyAsync,
                CanExecuteAction);

        ToggleFavoriteCommand =
            new AsyncRelayCommand(
                ToggleFavoriteAsync,
                () => CanExecuteAction() && ProviderMediaPolicy.AllowsPersistentLibrary(ProviderId));

        StartPreviewCommand =
            new AsyncRelayCommand(
                StartPreviewAsync,
                CanStartPreview);

        StopPreviewCommand =
            new RelayCommand(
                StopPreview,
                CanStopPreview);
    }

    private CopyGIF.Core.Settings.GifQuality _displayQuality = CopyGIF.Core.Settings.GifQuality.Medium;
    public CopyGIF.Core.Settings.GifQuality DisplayQuality
    {
        get => _displayQuality;
        set { if (SetProperty(ref _displayQuality, value)) OnPropertyChanged(nameof(DecodePixelSize)); }
    }
    public bool DecodeByHeight => Item.Height > Item.Width;
    public int DecodePixelSize => DisplayQuality switch
    {
        CopyGIF.Core.Settings.GifQuality.Minimum => 128,
        CopyGIF.Core.Settings.GifQuality.Low => 192,
        CopyGIF.Core.Settings.GifQuality.Medium => 256,
        CopyGIF.Core.Settings.GifQuality.High => 384,
        CopyGIF.Core.Settings.GifQuality.Maximum => 512,
        _ => 256
    };

    public GifItem Item { get; }

    public IAsyncRelayCommand LoadThumbnailCommand { get; }

    public IAsyncRelayCommand CopyCommand { get; }

    public IAsyncRelayCommand ToggleFavoriteCommand { get; }

    public IAsyncRelayCommand StartPreviewCommand { get; }

    public IRelayCommand StopPreviewCommand { get; }

    public GifIdentity Identity =>
        Item.StableIdentity;

    public string ProviderId =>
        Item.ProviderId;

    public string Id =>
        Item.Id;

    public string Title =>
        Item.Title;

    public string Description =>
        Item.Description;

    public Uri GifUri =>
        Item.GifUri;

    public Uri ThumbnailUri =>
        Item.ThumbnailUri;

    public Uri? PreviewUri =>
        Item.PreviewUri;

    public Uri? SourcePageUri =>
        Item.SourcePageUri;

    public int Width =>
        Item.Width;

    public int Height =>
        Item.Height;

    public long? SizeBytes =>
        Item.SizeBytes;

    public string? SearchQuery =>
        _searchQuery;

    public Uri? ThumbnailSource
    {
        get => _thumbnailSource;

        private set =>
            SetProperty(
                ref _thumbnailSource,
                value);
    }

    public Uri? CurrentSource
    {
        get => _currentSource;

        private set =>
            SetProperty(
                ref _currentSource,
                value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;

        private set
        {
            if (SetProperty(
                    ref _isFavorite,
                    value))
            {
                OnPropertyChanged(
                    nameof(FavoriteActionText));
            }
        }
    }

    public string FavoriteActionText =>
        IsFavorite
            ? "Remove from Favorites"
            : "Add to Favorites";

    public bool IsPreviewing
    {
        get => _isPreviewing;

        private set
        {
            if (SetProperty(
                    ref _isPreviewing,
                    value))
            {
                NotifyPreviewCommandStates();
            }
        }
    }

    public bool IsPreviewLoading
    {
        get => _isPreviewLoading;

        private set
        {
            if (SetProperty(
                    ref _isPreviewLoading,
                    value))
            {
                NotifyPreviewCommandStates();
            }
        }
    }

    public bool ReducedMotion
    {
        get => _reducedMotion;

        set
        {
            if (SetProperty(
                    ref _reducedMotion,
                    value) &&
                IsPreviewing)
            {
                StopPreview();
            }
        }
    }

    public AsyncOperationState OperationState
    {
        get => _operationState;

        private set
        {
            if (SetProperty(
                    ref _operationState,
                    value))
            {
                OnPropertyChanged(
                    nameof(IsBusy));

                NotifyActionCommandStates();
            }
        }
    }

    public bool IsBusy =>
        OperationState.IsBusy;

    public UserMessage? Message
    {
        get => _message;

        private set =>
            SetProperty(
                ref _message,
                value);
    }

    private bool _hasThumbnail;

    public async Task LoadThumbnailAsync(
        CancellationToken cancellationToken = default)
    {
        if (_hasThumbnail) return;
        try
        {
            Uri source =
                await _previewCoordinator
                    .GetThumbnailSourceAsync(
                        Item,
                        cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            _hasThumbnail = true;
            ThumbnailSource = source;
            OnPropertyChanged(nameof(ThumbnailSource));
            Message = null;

            if (!IsPreviewing)
            {
                CurrentSource =
                    source;
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            RepairDiagnostics.Record("thumbnail-resolve", ProviderId, exception.GetType().Name);
            Message = UserMessage.Warning("Preview unavailable. Select Retry preview to try again.");
            // A remote KLIPY URL cannot be decoded by the local-only card.
            ThumbnailSource = null;

            if (!IsPreviewing)
            {
                CurrentSource = null;
            }
        }
    }

    public void SetFavoriteState(
        bool isFavorite)
    {
        IsFavorite =
            isFavorite;
    }

    public void ClearMessage()
    {
        Message =
            null;
    }

    private bool CanExecuteAction()
    {
        return !IsBusy;
    }

    private bool CanStartPreview()
    {
        return
            !IsPreviewing &&
            !IsPreviewLoading;
    }

    private bool CanStopPreview()
    {
        return
            IsPreviewing ||
            IsPreviewLoading;
    }

    private async Task CopyAsync(
        CancellationToken cancellationToken)
    {
        RepairDiagnostics.Record("copy-click", ProviderId, "started");
        BeginOperation(
            "Copying GIF...");

        try
        {
            await _copyCoordinator
                .CopyAsync(
                    Item,
                    _searchQuery,
                    cancellationToken);

            OperationState =
                AsyncOperationState.Succeeded(
                    "GIF copied.");

            Message =
                UserMessage.Success(
                    "GIF copied to the clipboard.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Copy cancelled.");

            Message =
                UserMessage.Information(
                    "GIF copy cancelled.");
        }
        catch (Exception exception)
        {
            string detail = exception is MediaDownloadException download
                ? $"The GIF download failed ({download.Failure}). Try another GIF or lower copy quality."
                : exception is System.ComponentModel.Win32Exception native
                    ? $"Windows rejected the clipboard operation (error {native.NativeErrorCode}). Try again."
                    : $"Copy failed ({exception.GetType().Name}, 0x{exception.HResult:X8}).";
            RepairDiagnostics.Record("copy-failed", ProviderId,
                $"{exception.GetType().Name}-0x{exception.HResult:X8}");
            OperationState = AsyncOperationState.Failed("Unable to copy GIF.");
            Message = UserMessage.Error(detail, "gif_copy_failed");
        }
    }

    private async Task ToggleFavoriteAsync(
        CancellationToken cancellationToken)
    {
        bool addFavorite =
            !IsFavorite;

        BeginOperation(
            addFavorite
                ? "Adding favorite..."
                : "Removing favorite...");

        try
        {
            if (addFavorite)
            {
                await _libraryCoordinator
                    .AddFavoriteAsync(
                        Item,
                        cancellationToken);
            }
            else
            {
                await _libraryCoordinator
                    .RemoveFavoriteAsync(
                        Item.StableIdentity,
                        cancellationToken);
            }

            IsFavorite =
                addFavorite;

            if (addFavorite)
            {
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Added to Favorites.");

                Message =
                    UserMessage.Success(
                        "GIF added to Favorites.");
            }
            else
            {
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Removed from Favorites.");

                Message =
                    UserMessage.Success(
                        "GIF removed from Favorites.");
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Favorite change cancelled.");

            Message =
                UserMessage.Information(
                    "Favorite change cancelled.");
        }
        catch (InvalidOperationException exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    exception.Message);

            Message =
                UserMessage.Warning(
                    exception.Message);
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to update Favorites.");

            Message =
                UserMessage.Error(
                    "Unable to update Favorites.");
        }
    }

    private async Task StartPreviewAsync(
        CancellationToken cancellationToken)
    {
        if (IsPreviewing ||
            IsPreviewLoading)
        {
            return;
        }

        IsPreviewLoading =
            true;

        try
        {
            Uri source =
                await _previewCoordinator
                    .GetAnimatedSourceAsync(
                        Item,
                        ReducedMotion,
                        cancellationToken);

            cancellationToken
                .ThrowIfCancellationRequested();

            CurrentSource =
                source;

            IsPreviewing =
                true;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            CurrentSource =
                ThumbnailSource;

            IsPreviewing =
                false;
        }
        catch (Exception)
        {
            CurrentSource =
                ThumbnailSource;

            IsPreviewing =
                false;

            Message =
                UserMessage.Warning(
                    "Animated preview is unavailable.");
        }
        finally
        {
            IsPreviewLoading =
                false;
        }
    }

    private void StopPreview()
    {
        StartPreviewCommand
            .Cancel();

        CurrentSource =
            ThumbnailSource;

        IsPreviewing =
            false;

        NotifyPreviewCommandStates();
    }

    private void BeginOperation(
        string message)
    {
        Message =
            null;

        OperationState =
            AsyncOperationState.Running(
                message);
    }

    private void NotifyActionCommandStates()
    {
        CopyCommand
            .NotifyCanExecuteChanged();

        ToggleFavoriteCommand
            .NotifyCanExecuteChanged();
    }

    private void NotifyPreviewCommandStates()
    {
        StartPreviewCommand
            .NotifyCanExecuteChanged();

        StopPreviewCommand
            .NotifyCanExecuteChanged();
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
