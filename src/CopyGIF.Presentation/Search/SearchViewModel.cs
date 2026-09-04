using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Library;
using CopyGIF.Application.Media;
using CopyGIF.Application.Search;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Common;

namespace CopyGIF.Presentation.Search;

public enum GifSearchMode
{
    None,
    Search,
    Trending
}

public sealed class SearchViewModel :
    ObservableObject,
    IDisposable
{
    private const int MaximumSuggestions = 8;

    private readonly IGifSearchCoordinator
        _searchCoordinator;

    private readonly ISearchSuggestionCoordinator
        _suggestionCoordinator;

    private readonly IGifCopyCoordinator
        _copyCoordinator;

    private readonly IGifLibraryCoordinator
        _libraryCoordinator;

    private readonly IPreviewCoordinator
        _previewCoordinator;

    private readonly HashSet<string>
        _resultIdentities =
            new(
                StringComparer.Ordinal);

    private CancellationTokenSource?
        _operationCancellation;

    private CancellationTokenSource?
        _suggestionCancellation;

    private string _query =
        string.Empty;

    private string? _activeQuery;

    private string? _continuationToken;

    private GifSearchMode _mode =
        GifSearchMode.None;

    private AsyncOperationState _operationState =
        AsyncOperationState.Idle;

    private UserMessage? _message;

    private bool _isSuggestionBusy;

    private bool _reducedMotion;

    private bool _disposed;

    public SearchViewModel(
        IGifSearchCoordinator searchCoordinator,
        ISearchSuggestionCoordinator suggestionCoordinator,
        IGifCopyCoordinator copyCoordinator,
        IGifLibraryCoordinator libraryCoordinator,
        IPreviewCoordinator previewCoordinator)
    {
        _searchCoordinator =
            searchCoordinator ??
            throw new ArgumentNullException(
                nameof(searchCoordinator));

        _suggestionCoordinator =
            suggestionCoordinator ??
            throw new ArgumentNullException(
                nameof(suggestionCoordinator));

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

        Results.CollectionChanged +=
            (_, _) =>
            {
                OnPropertyChanged(
                    nameof(ResultCount));

                OnPropertyChanged(
                    nameof(HasResults));
            };

        Suggestions.CollectionChanged +=
            (_, _) =>
            {
                OnPropertyChanged(
                    nameof(SuggestionCount));

                OnPropertyChanged(
                    nameof(HasSuggestions));
            };

        SearchCommand =
            new AsyncRelayCommand(
                cancellationToken =>
                    ExecuteQuerySearchAsync(
                        false,
                        cancellationToken),
                CanSearch);

        SearchDebouncedCommand =
            new AsyncRelayCommand(
                cancellationToken =>
                    ExecuteQuerySearchAsync(
                        true,
                        cancellationToken),
                CanSearch);

        TrendingCommand =
            new AsyncRelayCommand(
                TrendingAsync,
                CanStartOperation);

        LoadMoreCommand =
            new AsyncRelayCommand(
                LoadMoreAsync,
                CanLoadMore);

        RefreshSuggestionsCommand =
            new AsyncRelayCommand(
                RefreshSuggestionsAsync,
                CanRefreshSuggestions);

        ClearSuggestionHistoryCommand =
            new AsyncRelayCommand(
                ClearSuggestionHistoryAsync,
                CanClearSuggestionHistory);

        ClearQueryCommand =
            new RelayCommand(
                ClearQuery,
                CanClearQuery);

        CancelCommand =
            new RelayCommand(
                CancelOperations,
                CanCancel);
    }

    public ObservableCollection<GifCardViewModel>
        Results
    { get; } =
        new();

    public ObservableCollection<string>
        Suggestions
    { get; } =
        new();

    public IAsyncRelayCommand SearchCommand
    { get; }

    public IAsyncRelayCommand SearchDebouncedCommand
    { get; }

    public IAsyncRelayCommand TrendingCommand
    { get; }

    public IAsyncRelayCommand LoadMoreCommand
    { get; }

    public IAsyncRelayCommand RefreshSuggestionsCommand
    { get; }

    public IAsyncRelayCommand ClearSuggestionHistoryCommand
    { get; }

    public IRelayCommand ClearQueryCommand
    { get; }

    public IRelayCommand CancelCommand
    { get; }

    public string Query
    {
        get => _query;

        set
        {
            string normalized =
                value ??
                string.Empty;

            if (SetProperty(
                    ref _query,
                    normalized))
            {
                OnPropertyChanged(
                    nameof(CanSubmitQuery));

                NotifyCommandStates();
            }
        }
    }

    public string? ActiveQuery =>
        _activeQuery;

    public GifSearchMode Mode
    {
        get => _mode;

        private set =>
            SetProperty(
                ref _mode,
                value);
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

    public bool IsSuggestionBusy
    {
        get => _isSuggestionBusy;

        private set
        {
            if (SetProperty(
                    ref _isSuggestionBusy,
                    value))
            {
                NotifyCommandStates();
            }
        }
    }

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
                     in Results)
            {
                card.ReducedMotion =
                    value;
            }
        }
    }

    public bool CanSubmitQuery =>
        !string.IsNullOrWhiteSpace(
            Query);

    public int ResultCount =>
        Results.Count;

    public bool HasResults =>
        Results.Count > 0;

    public int SuggestionCount =>
        Suggestions.Count;

    public bool HasSuggestions =>
        Suggestions.Count > 0;

    public bool HasMoreResults =>
        !string.IsNullOrWhiteSpace(
            _continuationToken);

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
        _suggestionCancellation?.Cancel();

        _operationCancellation =
            null;

        _suggestionCancellation =
            null;

        foreach (GifCardViewModel card
                 in Results)
        {
            card.StopPreviewCommand
                .Execute(null);
        }
    }

    private bool CanSearch()
    {
        return
            !IsBusy &&
            CanSubmitQuery;
    }

    private bool CanStartOperation()
    {
        return !IsBusy;
    }

    private bool CanLoadMore()
    {
        if (IsBusy ||
            string.IsNullOrWhiteSpace(
                _continuationToken))
        {
            return false;
        }

        return Mode switch
        {
            GifSearchMode.Search =>
                !string.IsNullOrWhiteSpace(
                    _activeQuery) &&
                string.Equals(
                    Query.Trim(),
                    _activeQuery,
                    StringComparison.Ordinal),

            GifSearchMode.Trending =>
                true,

            _ =>
                false
        };
    }

    private bool CanRefreshSuggestions()
    {
        return
            !IsBusy &&
            !IsSuggestionBusy;
    }

    private bool CanClearSuggestionHistory()
    {
        return
            !IsBusy &&
            !IsSuggestionBusy;
    }

    private bool CanClearQuery()
    {
        return
            !IsBusy &&
            !string.IsNullOrEmpty(
                Query);
    }

    private bool CanCancel()
    {
        return
            IsBusy ||
            IsSuggestionBusy;
    }

    private async Task ExecuteQuerySearchAsync(
        bool useDebounce,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        string searchQuery =
            Query.Trim();

        if (string.IsNullOrWhiteSpace(
                searchQuery))
        {
            return;
        }

        CancellationTokenSource operation =
            BeginOperation(
                "Searching...",
                cancellationToken);

        Mode =
            GifSearchMode.Search;

        _activeQuery =
            searchQuery;

        _continuationToken =
            null;

        ClearResults();

        NotifyPaginationState();

        try
        {
            GifSearchPage page =
                useDebounce
                    ? await _searchCoordinator
                        .SearchDebouncedAsync(
                            searchQuery,
                            operation.Token)
                    : await _searchCoordinator
                        .SearchAsync(
                            searchQuery,
                            operation.Token);

            HashSet<string> favorites =
                await LoadFavoriteIdentitiesAsync(
                    operation.Token);

            ApplyPage(
                page,
                favorites,
                searchQuery);

            SetSuccessfulResultState();
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Search cancelled.");

            Message =
                UserMessage.Information(
                    "Search cancelled.");
        }
        catch (GifProviderException exception)
        {
            ApplyProviderFailure(
                exception);
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "GIF search failed.");

            Message =
                UserMessage.Error(
                    "Unable to search for GIFs.",
                    "search_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task TrendingAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        CancellationTokenSource operation =
            BeginOperation(
                "Loading Trending GIFs...",
                cancellationToken);

        Mode =
            GifSearchMode.Trending;

        _activeQuery =
            null;

        _continuationToken =
            null;

        ClearResults();

        NotifyPaginationState();

        try
        {
            GifSearchPage page =
                await _searchCoordinator
                    .TrendingAsync(
                        operation.Token);

            HashSet<string> favorites =
                await LoadFavoriteIdentitiesAsync(
                    operation.Token);

            ApplyPage(
                page,
                favorites,
                searchQuery: null);

            SetSuccessfulResultState();
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Trending request cancelled.");

            Message =
                UserMessage.Information(
                    "Trending request cancelled.");
        }
        catch (GifProviderException exception)
        {
            ApplyProviderFailure(
                exception);
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to load Trending GIFs.");

            Message =
                UserMessage.Error(
                    "Unable to load Trending GIFs.",
                    "trending_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task LoadMoreAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(
                _continuationToken))
        {
            return;
        }

        string continuationToken =
            _continuationToken;

        CancellationTokenSource operation =
            BeginOperation(
                "Loading more GIFs...",
                cancellationToken);

        try
        {
            GifSearchPage page;

            switch (Mode)
            {
                case GifSearchMode.Search:
                    if (string.IsNullOrWhiteSpace(
                            _activeQuery))
                    {
                        return;
                    }

                    page =
                        await _searchCoordinator
                            .LoadMoreAsync(
                                _activeQuery,
                                continuationToken,
                                operation.Token);

                    break;

                case GifSearchMode.Trending:
                    page =
                        await _searchCoordinator
                            .LoadMoreTrendingAsync(
                                continuationToken,
                                operation.Token);

                    break;

                default:
                    return;
            }

            HashSet<string> favorites =
                await LoadFavoriteIdentitiesAsync(
                    operation.Token);

            ApplyPage(
                page,
                favorites,
                Mode == GifSearchMode.Search
                    ? _activeQuery
                    : null);

            SetSuccessfulResultState();
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Load more cancelled.");

            Message =
                UserMessage.Information(
                    "Loading additional GIFs was cancelled.");
        }
        catch (GifProviderException exception)
        {
            ApplyProviderFailure(
                exception);
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to load more GIFs.");

            Message =
                UserMessage.Error(
                    "Unable to load more GIFs.",
                    "load_more_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task RefreshSuggestionsAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        string input =
            Query.Trim();

        CancelSuggestionOperation();

        if (string.IsNullOrWhiteSpace(
                input))
        {
            Suggestions.Clear();

            return;
        }

        CancellationTokenSource operation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        _suggestionCancellation =
            operation;

        IsSuggestionBusy =
            true;

        try
        {
            IReadOnlyList<string> suggestions =
                await _suggestionCoordinator
                    .GetSuggestionsAsync(
                        input,
                        MaximumSuggestions,
                        operation.Token);

            if (!ReferenceEquals(
                    _suggestionCancellation,
                    operation))
            {
                return;
            }

            Suggestions.Clear();

            foreach (string suggestion
                     in suggestions
                         .Where(
                             value =>
                                 !string.IsNullOrWhiteSpace(
                                     value))
                         .Select(
                             value =>
                                 value.Trim())
                         .Distinct(
                             StringComparer.OrdinalIgnoreCase)
                         .Take(
                             MaximumSuggestions))
            {
                Suggestions.Add(
                    suggestion);
            }
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            Message =
                UserMessage.Warning(
                    "Search suggestions are temporarily unavailable.",
                    "suggestions_failed");
        }
        finally
        {
            if (ReferenceEquals(
                    _suggestionCancellation,
                    operation))
            {
                _suggestionCancellation =
                    null;

                IsSuggestionBusy =
                    false;
            }

            operation.Dispose();

            NotifyCommandStates();
        }
    }

    private async Task ClearSuggestionHistoryAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        CancelSuggestionOperation();

        CancellationTokenSource operation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        _suggestionCancellation =
            operation;

        IsSuggestionBusy =
            true;

        try
        {
            await _suggestionCoordinator
                .ClearHistoryAsync(
                    operation.Token);

            Suggestions.Clear();

            Message =
                UserMessage.Success(
                    "Search history cleared.");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            Message =
                UserMessage.Error(
                    "Unable to clear search history.",
                    "clear_search_history_failed");
        }
        finally
        {
            if (ReferenceEquals(
                    _suggestionCancellation,
                    operation))
            {
                _suggestionCancellation =
                    null;

                IsSuggestionBusy =
                    false;
            }

            operation.Dispose();

            NotifyCommandStates();
        }
    }

    private void ClearQuery()
    {
        Query =
            string.Empty;

        _activeQuery =
            null;

        _continuationToken =
            null;

        Mode =
            GifSearchMode.None;

        Suggestions.Clear();

        ClearResults();

        OperationState =
            AsyncOperationState.Idle;

        Message =
            null;

        NotifyPaginationState();
    }

    private CancellationTokenSource BeginOperation(
        string message,
        CancellationToken cancellationToken)
    {
        _operationCancellation?.Cancel();

        CancellationTokenSource operation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        _operationCancellation =
            operation;

        Message =
            null;

        OperationState =
            AsyncOperationState.Running(
                message);

        return operation;
    }

    private void EndOperation(
        CancellationTokenSource operation)
    {
        if (ReferenceEquals(
                _operationCancellation,
                operation))
        {
            _operationCancellation =
                null;
        }

        operation.Dispose();

        NotifyCommandStates();
    }

    private void CancelOperations()
    {
        _operationCancellation?.Cancel();
        _suggestionCancellation?.Cancel();
    }

    private void CancelSuggestionOperation()
    {
        CancellationTokenSource? previous =
            _suggestionCancellation;

        _suggestionCancellation =
            null;

        previous?.Cancel();

        IsSuggestionBusy =
            false;
    }

    private async Task<HashSet<string>>
        LoadFavoriteIdentitiesAsync(
            CancellationToken cancellationToken)
    {
        try
        {
            LibrarySnapshot snapshot =
                await _libraryCoordinator
                    .LoadAsync(
                        cancellationToken);

            return snapshot.Favorites
                .Select(
                    entry =>
                        entry.Identity.ToString())
                .ToHashSet(
                    StringComparer.Ordinal);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new HashSet<string>(
                StringComparer.Ordinal);
        }
    }

    private void ApplyPage(
        GifSearchPage page,
        HashSet<string> favoriteIdentities,
        string? searchQuery)
    {
        ArgumentNullException.ThrowIfNull(
            page);

        foreach (GifItem item
                 in page.Items)
        {
            if (!_resultIdentities.Add(
                    item.Identity))
            {
                continue;
            }

            Results.Add(
                new GifCardViewModel(
                    item,
                    _copyCoordinator,
                    _libraryCoordinator,
                    _previewCoordinator,
                    favoriteIdentities.Contains(
                        item.Identity),
                    searchQuery,
                    ReducedMotion));
        }

        _continuationToken =
            page.ContinuationToken;

        NotifyPaginationState();
    }

    private void SetSuccessfulResultState()
    {
        if (Results.Count == 0)
        {
            OperationState =
                AsyncOperationState.Succeeded(
                    Mode == GifSearchMode.Trending
                        ? "No Trending GIFs available."
                        : "No GIFs found.");

            Message =
                UserMessage.Information(
                    Mode == GifSearchMode.Trending
                        ? "No Trending GIFs are currently available."
                        : "No GIFs matched your search.");

            return;
        }

        string status =
            Results.Count switch
            {
                1 when HasMoreResults =>
                    "1 GIF found.",

                1 =>
                    "1 GIF found. End of results.",

                _ when HasMoreResults =>
                    $"{Results.Count} GIFs found.",

                _ =>
                    $"{Results.Count} GIFs found. End of results."
            };

        OperationState =
            AsyncOperationState.Succeeded(
                status);

        Message =
            null;
    }

    private void ApplyProviderFailure(
        GifProviderException exception)
    {
        UserMessage message =
            exception.Failure switch
            {
                GifProviderFailure.MissingCredential =>
                    UserMessage.Warning(
                        "A GIF provider API key is required.",
                        "missing_credential"),

                GifProviderFailure.Unauthorized =>
                    UserMessage.Error(
                        "The configured GIF provider API key was rejected.",
                        "unauthorized"),

                GifProviderFailure.RateLimited =>
                    UserMessage.Warning(
                        "GIF searches are temporarily rate limited.",
                        "rate_limited"),

                GifProviderFailure.Network =>
                    UserMessage.Warning(
                        "Unable to reach the GIF provider. Check your connection.",
                        "network"),

                GifProviderFailure.Timeout =>
                    UserMessage.Warning(
                        "The GIF provider took too long to respond.",
                        "timeout"),

                GifProviderFailure.ServiceUnavailable =>
                    UserMessage.Warning(
                        "The GIF provider is temporarily unavailable.",
                        "service_unavailable"),

                GifProviderFailure.InvalidResponse =>
                    UserMessage.Error(
                        "The GIF provider returned an unexpected response.",
                        "invalid_response"),

                _ =>
                    UserMessage.Error(
                        "GIF search failed.",
                        "provider_failure")
            };

        OperationState =
            AsyncOperationState.Failed(
                message.Text);

        Message =
            message;
    }

    private void ClearResults()
    {
        foreach (GifCardViewModel card
                 in Results)
        {
            card.StopPreviewCommand
                .Execute(null);
        }

        Results.Clear();
        _resultIdentities.Clear();
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

        SearchDebouncedCommand
            .NotifyCanExecuteChanged();

        TrendingCommand
            .NotifyCanExecuteChanged();

        LoadMoreCommand
            .NotifyCanExecuteChanged();

        RefreshSuggestionsCommand
            .NotifyCanExecuteChanged();

        ClearSuggestionHistoryCommand
            .NotifyCanExecuteChanged();

        ClearQueryCommand
            .NotifyCanExecuteChanged();

        CancelCommand
            .NotifyCanExecuteChanged();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}
