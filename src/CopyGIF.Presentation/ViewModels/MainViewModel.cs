using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Search;
using CopyGIF.Core.Models;

namespace CopyGIF.Presentation.ViewModels;

public sealed class MainViewModel :
    ObservableObject
{
    private readonly IGifSearchCoordinator
        _searchCoordinator;

    private readonly HashSet<string>
        _resultIdentities =
            new(StringComparer.Ordinal);

    private CancellationTokenSource?
        _operationCancellation;

    private string _query =
        string.Empty;

    private string? _activeQuery;

    private string? _continuationToken;

    private bool _isBusy;

    private string _statusMessage =
        "Ready";

    public MainViewModel(
        IGifSearchCoordinator searchCoordinator)
    {
        _searchCoordinator =
            searchCoordinator ??
            throw new ArgumentNullException(
                nameof(searchCoordinator));

        Results.CollectionChanged +=
            (_, _) =>
            {
                OnPropertyChanged(
                    nameof(ResultCount));

                OnPropertyChanged(
                    nameof(HasResults));
            };

        SearchCommand =
            new AsyncRelayCommand(
                SearchAsync,
                CanSearch);

        LoadMoreCommand =
            new AsyncRelayCommand(
                LoadMoreAsync,
                CanLoadMore);

        CancelCommand =
            new RelayCommand(
                CancelOperation,
                CanCancel);
    }

    public ObservableCollection<GifItem>
        Results
    { get; } =
            new();

    public IAsyncRelayCommand
        SearchCommand
    { get; }

    public IAsyncRelayCommand
        LoadMoreCommand
    { get; }

    public IRelayCommand
        CancelCommand
    { get; }

    public string Query
    {
        get => _query;

        set
        {
            if (SetProperty(
                    ref _query,
                    value))
            {
                NotifyCommandStates();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;

        private set
        {
            if (SetProperty(
                    ref _isBusy,
                    value))
            {
                NotifyCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;

        private set =>
            SetProperty(
                ref _statusMessage,
                value);
    }

    public int ResultCount =>
        Results.Count;

    public bool HasResults =>
        Results.Count > 0;

    public bool HasMoreResults =>
        !string.IsNullOrWhiteSpace(
            _continuationToken);

    private bool CanSearch()
    {
        return
            !IsBusy &&
            !string.IsNullOrWhiteSpace(
                Query);
    }

    private bool CanLoadMore()
    {
        if (IsBusy)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                _activeQuery))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(
                _continuationToken))
        {
            return false;
        }

        return string.Equals(
            Query.Trim(),
            _activeQuery,
            StringComparison.Ordinal);
    }

    private bool CanCancel()
    {
        return IsBusy;
    }

    private void CancelOperation()
    {
        _operationCancellation?.Cancel();
    }

    private async Task SearchAsync()
    {
        string searchQuery =
            Query.Trim();

        if (string.IsNullOrWhiteSpace(
                searchQuery))
        {
            return;
        }

        CancellationTokenSource cancellation =
            BeginOperation(
                "Searching...");

        _activeQuery =
            searchQuery;

        _continuationToken =
            null;

        Results.Clear();

        _resultIdentities.Clear();

        NotifyPaginationState();

        try
        {
            GifSearchPage page =
                await _searchCoordinator
                    .SearchAsync(
                        searchQuery,
                        cancellation.Token);

            ApplyPage(
                page);

            UpdateResultStatus();
        }
        catch (OperationCanceledException)
            when (
                cancellation
                    .IsCancellationRequested)
        {
            StatusMessage =
                "Search cancelled.";
        }
        catch (GifProviderException exception)
        {
            StatusMessage =
                GetProviderErrorMessage(
                    exception.Failure);
        }
        catch (Exception)
        {
            StatusMessage =
                "GIF search failed.";
        }
        finally
        {
            EndOperation(
                cancellation);
        }
    }

    private async Task LoadMoreAsync()
    {
        if (string.IsNullOrWhiteSpace(
                _activeQuery) ||
            string.IsNullOrWhiteSpace(
                _continuationToken))
        {
            return;
        }

        string activeQuery =
            _activeQuery;

        string continuationToken =
            _continuationToken;

        CancellationTokenSource cancellation =
            BeginOperation(
                "Loading more...");

        try
        {
            GifSearchPage page =
                await _searchCoordinator
                    .LoadMoreAsync(
                        activeQuery,
                        continuationToken,
                        cancellation.Token);

            ApplyPage(
                page);

            UpdateResultStatus();
        }
        catch (OperationCanceledException)
            when (
                cancellation
                    .IsCancellationRequested)
        {
            StatusMessage =
                "Load more cancelled.";
        }
        catch (GifProviderException exception)
        {
            StatusMessage =
                GetProviderErrorMessage(
                    exception.Failure);
        }
        catch (Exception)
        {
            StatusMessage =
                "Unable to load more GIFs.";
        }
        finally
        {
            EndOperation(
                cancellation);
        }
    }

    private CancellationTokenSource
        BeginOperation(
            string statusMessage)
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();

        CancellationTokenSource cancellation =
            new();

        _operationCancellation =
            cancellation;

        IsBusy =
            true;

        StatusMessage =
            statusMessage;

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

            IsBusy =
                false;
        }

        cancellation.Dispose();

        NotifyCommandStates();
    }

    private void ApplyPage(
        GifSearchPage page)
    {
        foreach (GifItem item
                 in page.Items)
        {
            if (_resultIdentities.Add(
                    item.Identity))
            {
                Results.Add(
                    item);
            }
        }

        _continuationToken =
            page.ContinuationToken;

        NotifyPaginationState();
    }

    private void UpdateResultStatus()
    {
        StatusMessage =
            Results.Count switch
            {
                0 =>
                    "No results found.",

                1 =>
                    HasMoreResults
                        ? "1 result"
                        : "1 result - end of results",

                _ =>
                    HasMoreResults
                        ? $"{Results.Count} results"
                        : $"{Results.Count} results - end of results"
            };
    }

    private void NotifyPaginationState()
    {
        OnPropertyChanged(
            nameof(HasMoreResults));

        LoadMoreCommand
            .NotifyCanExecuteChanged();
    }

    private void NotifyCommandStates()
    {
        SearchCommand
            .NotifyCanExecuteChanged();

        LoadMoreCommand
            .NotifyCanExecuteChanged();

        CancelCommand
            .NotifyCanExecuteChanged();
    }

    private static string
        GetProviderErrorMessage(
            GifProviderFailure failure)
    {
        return failure switch
        {
            GifProviderFailure.MissingCredential =>
                "A GIF provider API key is required.",

            GifProviderFailure.Unauthorized =>
                "The configured GIF provider rejected its API key.",

            GifProviderFailure.RateLimited =>
                "GIF searches are temporarily rate limited.",

            GifProviderFailure.Network =>
                "Unable to reach the GIF provider. Check your connection.",

            GifProviderFailure.ServiceUnavailable =>
                "The GIF provider is temporarily unavailable.",

            GifProviderFailure.InvalidResponse =>
                "The GIF provider returned an unexpected response.",

            _ =>
                "GIF search failed."
        };
    }
}