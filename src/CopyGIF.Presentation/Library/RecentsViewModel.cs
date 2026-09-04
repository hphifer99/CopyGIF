using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Library;
using CopyGIF.Application.Media;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Search;

namespace CopyGIF.Presentation.Library;

public sealed class RecentsViewModel :
    ObservableObject,
    IDisposable
{
    private readonly IGifLibraryCoordinator
        _libraryCoordinator;

    private readonly IGifCopyCoordinator
        _copyCoordinator;

    private readonly IPreviewCoordinator
        _previewCoordinator;

    private CancellationTokenSource?
        _operationCancellation;

    private AsyncOperationState
        _operationState =
            AsyncOperationState.Idle;

    private UserMessage? _message;

    private bool _reducedMotion;

    private bool _disposed;

    public RecentsViewModel(
        IGifLibraryCoordinator libraryCoordinator,
        IGifCopyCoordinator copyCoordinator,
        IPreviewCoordinator previewCoordinator)
    {
        _libraryCoordinator =
            libraryCoordinator ??
            throw new ArgumentNullException(
                nameof(libraryCoordinator));

        _copyCoordinator =
            copyCoordinator ??
            throw new ArgumentNullException(
                nameof(copyCoordinator));

        _previewCoordinator =
            previewCoordinator ??
            throw new ArgumentNullException(
                nameof(previewCoordinator));

        Items.CollectionChanged +=
            (_, _) =>
            {
                OnPropertyChanged(
                    nameof(Count));

                OnPropertyChanged(
                    nameof(HasItems));

                NotifyCommandStates();
            };

        LoadCommand =
            new AsyncRelayCommand(
                LoadAsync,
                CanStartOperation);

        ClearCommand =
            new AsyncRelayCommand(
                ClearAsync,
                CanClear);

        CancelCommand =
            new RelayCommand(
                CancelOperation,
                CanCancel);
    }

    public ObservableCollection<GifCardViewModel>
        Items
    { get; } =
        new();

    public IAsyncRelayCommand LoadCommand
    { get; }

    public IAsyncRelayCommand ClearCommand
    { get; }

    public IRelayCommand CancelCommand
    { get; }

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

                NotifyCommandStates();
            }
        }
    }

    public UserMessage? Message
    {
        get => _message;

        private set =>
            SetProperty(
                ref _message,
                value);
    }

    public bool IsBusy =>
        OperationState.IsBusy;

    public bool ReducedMotion
    {
        get => _reducedMotion;

        set
        {
            if (!SetProperty(
                    ref _reducedMotion,
                    value))
            {
                return;
            }

            foreach (GifCardViewModel card
                     in Items)
            {
                card.ReducedMotion =
                    value;
            }
        }
    }

    public int Count =>
        Items.Count;

    public bool HasItems =>
        Items.Count > 0;

    public void ClearMessage()
    {
        Message =
            null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();

        _operationCancellation =
            null;

        ClearItems();
    }

    private bool CanStartOperation()
    {
        return !IsBusy;
    }

    private bool CanClear()
    {
        return
            !IsBusy &&
            HasItems;
    }

    private bool CanCancel()
    {
        return IsBusy;
    }

    private async Task LoadAsync()
    {
        ThrowIfDisposed();

        CancellationTokenSource cancellation =
            BeginOperation(
                "Loading Recents...");

        try
        {
            LibrarySnapshot snapshot =
                await _libraryCoordinator
                    .LoadAsync(
                        cancellation.Token);

            ApplySnapshot(
                snapshot);

            string status =
                Count switch
                {
                    0 =>
                        "No Recent GIFs.",

                    1 =>
                        "1 Recent GIF.",

                    _ =>
                        $"{Count} Recent GIFs."
                };

            OperationState =
                AsyncOperationState.Succeeded(
                    status);

            Message =
                Count == 0
                    ? UserMessage.Information(
                        "Copied GIFs will appear here.")
                    : null;
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Recents load cancelled.");

            Message =
                UserMessage.Information(
                    "Loading Recents was cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to load Recents.");

            Message =
                UserMessage.Error(
                    "Unable to load Recents.",
                    "recents_load_failed");
        }
        finally
        {
            EndOperation(
                cancellation);
        }
    }

    private async Task ClearAsync()
    {
        ThrowIfDisposed();

        if (!HasItems)
        {
            return;
        }

        CancellationTokenSource cancellation =
            BeginOperation(
                "Clearing Recents...");

        try
        {
            await _libraryCoordinator
                .ClearRecentsAsync(
                    cancellation.Token);

            ClearItems();

            OperationState =
                AsyncOperationState.Succeeded(
                    "Recents cleared.");

            Message =
                UserMessage.Success(
                    "Recents cleared.");
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Clear Recents cancelled.");

            Message =
                UserMessage.Information(
                    "Clearing Recents was cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to clear Recents.");

            Message =
                UserMessage.Error(
                    "Unable to clear Recents.",
                    "recents_clear_failed");
        }
        finally
        {
            EndOperation(
                cancellation);
        }
    }

    private void ApplySnapshot(
        LibrarySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        HashSet<string> favorites =
            snapshot.Favorites
                .Select(
                    entry =>
                        entry.Identity.ToString())
                .ToHashSet(
                    StringComparer.Ordinal);

        ClearItems();

        foreach (LibraryEntry entry
                 in snapshot.Recents)
        {
            GifItem item =
                CreateGifItem(
                    entry);

            GifCardViewModel card =
                new(
                    item,
                    _copyCoordinator,
                    _libraryCoordinator,
                    _previewCoordinator,
                    favorites.Contains(
                        item.Identity),
                    searchQuery: null,
                    reducedMotion:
                        ReducedMotion);

            Items.Add(
                card);
        }
    }

    private void ClearItems()
    {
        foreach (GifCardViewModel card
                 in Items)
        {
            card.StopPreviewCommand
                .Execute(null);
        }

        Items.Clear();
    }

    private CancellationTokenSource BeginOperation(
        string message)
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();

        CancellationTokenSource cancellation =
            new();

        _operationCancellation =
            cancellation;

        Message =
            null;

        OperationState =
            AsyncOperationState.Running(
                message);

        return cancellation;
    }

    private void EndOperation(
        CancellationTokenSource cancellation)
    {
        if (ReferenceEquals(
                _operationCancellation,
                cancellation))
        {
            _operationCancellation =
                null;
        }

        cancellation.Dispose();

        NotifyCommandStates();
    }

    private void CancelOperation()
    {
        _operationCancellation?.Cancel();
    }

    private void NotifyCommandStates()
    {
        LoadCommand
            .NotifyCanExecuteChanged();

        ClearCommand
            .NotifyCanExecuteChanged();

        CancelCommand
            .NotifyCanExecuteChanged();
    }

    private static GifItem CreateGifItem(
        LibraryEntry entry)
    {
        return new GifItem
        {
            ProviderId =
                entry.Identity.ProviderId,

            Id =
                entry.Identity.Id,

            Title =
                entry.Title,

            Description =
                entry.Description,

            ThumbnailUri =
                entry.ThumbnailUri,

            GifUri =
                entry.GifUri,

            PreviewUri =
                entry.PreviewUri,

            SourcePageUri =
                entry.SourcePageUri,

            Width =
                entry.Width,

            Height =
                entry.Height,

            SizeBytes =
                entry.SizeBytes
        };
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}
