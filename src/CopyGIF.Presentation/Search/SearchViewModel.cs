using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Library;
using CopyGIF.Application.Media;
using CopyGIF.Application.Search;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
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
    private bool _isLoadingMore;
    public bool IsLoadingMore
    {
        get => _isLoadingMore;
        private set => SetProperty(ref _isLoadingMore, value);
    }

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
    private AppSettings _settings = new();
    private GifSearchPage? _trendingSnapshot;
    private Task? _emptyQueryOperation;
    public bool AutoLoadMoreResults => _settings.Search.AutoLoadMoreResults;
    public string ActiveProviderId => _settings.Providers.ActiveProviderId;
    public bool IsGiphy => ActiveProviderId == "giphy";
    public string AttributionText => ActiveProviderId == "giphy" ? "Powered By GIPHY" : "Powered by KLIPY";
    public double TrendingScrollOffset { get; set; }

    public void Configure(AppSettings settings)
    {
        bool providerChanged = !string.Equals(ActiveProviderId, settings.Providers.ActiveProviderId, StringComparison.OrdinalIgnoreCase);
        bool emptyModeChanged = _settings.Search.ShowTrendingWhenEmpty != settings.Search.ShowTrendingWhenEmpty;
        bool pageSizeChanged = _settings.Search.ResultsPerSearch != settings.Search.ResultsPerSearch;
        bool ratingChanged = _settings.Search.ContentRating != settings.Search.ContentRating;
        _settings = settings;
        OnPropertyChanged(nameof(AutoLoadMoreResults));
        OnPropertyChanged(nameof(ActiveProviderId));
        OnPropertyChanged(nameof(AttributionText));
        OnPropertyChanged(nameof(IsGiphy));
        if (providerChanged || pageSizeChanged || ratingChanged) { _trendingSnapshot = null; TrendingScrollOffset = 0; }
        if (providerChanged || ratingChanged)
        {
            _operationCancellation?.Cancel();
            _operationCancellation = null;
            ClearResults();
            _continuationToken = null;
            OperationState = AsyncOperationState.Idle;
            Mode = GifSearchMode.None;
            if (string.IsNullOrWhiteSpace(Query)) ClearQuery();
            else SearchCommand.Execute(null);
        }
        else if (emptyModeChanged && string.IsNullOrWhiteSpace(Query)) ClearQuery();
    }


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
                CanSearch,
                AsyncRelayCommandOptions.AllowConcurrentExecutions);

        SearchDebouncedCommand =
            new AsyncRelayCommand(
                cancellationToken =>
                    ExecuteQuerySearchAsync(
                        true,
                        cancellationToken),
                CanSearch,
                AsyncRelayCommandOptions.AllowConcurrentExecutions);

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
                CanRefreshSuggestions,
                AsyncRelayCommandOptions.AllowConcurrentExecutions);

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
                _operationCancellation?.Cancel();
                CancelSuggestionOperation();

                if (string.IsNullOrWhiteSpace(normalized))
                {
                    ClearQuery();
                }

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

    private CopyGIF.Core.Settings.GifQuality _displayQuality = CopyGIF.Core.Settings.GifQuality.Medium;
    public CopyGIF.Core.Settings.GifQuality DisplayQuality
    {
        get => _displayQuality;
        set
        {
            if (!SetProperty(ref _displayQuality, value)) return;
            foreach (GifCardViewModel card in Results) card.DisplayQuality = value;
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
            card.PropertyChanged -= OnCardPropertyChanged;
            card.StopPreviewCommand
                .Execute(null);
        }
    }

    private bool CanSearch()
    {
        return !_disposed && CanSubmitQuery;
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
        return !_disposed;
    }

    private bool CanClearSuggestionHistory()
    {
        return
            !IsBusy &&
            !IsSuggestionBusy;
    }

    private bool CanClearQuery()
    {
        return !_disposed &&
            (!string.IsNullOrEmpty(Query) || HasResults || IsBusy || HasMoreResults);
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

            operation.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_operationCancellation, operation) || _disposed)
            {
                return;
            }

            ClearResults();
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
            if (!ReferenceEquals(_operationCancellation, operation) || _disposed)
            {
                return;
            }

            OperationState =
                AsyncOperationState.Cancelled(
                    "Search cancelled.");

            Message =
                UserMessage.Information(
                    "Search cancelled.");
        }
        catch (GifProviderException exception)
        {
            if (!ReferenceEquals(_operationCancellation, operation) || _disposed)
            {
                return;
            }

            ApplyProviderFailure(
                exception);
        }
        catch (Exception)
        {
            if (!ReferenceEquals(_operationCancellation, operation) || _disposed)
            {
                return;
            }

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
        if (!string.IsNullOrWhiteSpace(Query)) return;
        if (!_settings.Search.ShowTrendingWhenEmpty) { ClearResults(); return; }
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
            GifSearchPage page = ActiveProviderId == "klipy" && _trendingSnapshot is not null
                ? _trendingSnapshot
                : await _searchCoordinator.TrendingAsync(operation.Token);

            HashSet<string> favorites =
                await LoadFavoriteIdentitiesAsync(
                    operation.Token);

            operation.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_operationCancellation, operation) || _disposed)
            {
                return;
            }

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
            if (!ReferenceEquals(_operationCancellation, operation) || _disposed)
            {
                return;
            }

            OperationState =
                AsyncOperationState.Cancelled(
                    "Trending request cancelled.");

            Message =
                UserMessage.Information(
                    "Trending request cancelled.");
        }
        catch (GifProviderException exception)
        {
            if (!ReferenceEquals(_operationCancellation, operation) || _disposed)
            {
                return;
            }

            ApplyProviderFailure(
                exception);
        }
        catch (Exception)
        {
            if (!ReferenceEquals(_operationCancellation, operation) || _disposed)
            {
                return;
            }

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

        IsLoadingMore = true;
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

            operation.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(_operationCancellation, operation) || _disposed)
            {
                return;
            }

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
            if (!ReferenceEquals(_operationCancellation, operation) || _disposed)
            {
                return;
            }

            OperationState =
                AsyncOperationState.Cancelled(
                    "Load more cancelled.");

            Message =
                UserMessage.Information(
                    "Loading additional GIFs was cancelled.");
        }
        catch (GifProviderException exception)
        {
            if (!ReferenceEquals(_operationCancellation, operation) || _disposed)
            {
                return;
            }

            ApplyProviderFailure(
                exception);
        }
        catch (Exception)
        {
            if (!ReferenceEquals(_operationCancellation, operation) || _disposed)
            {
                return;
            }

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
            IsLoadingMore = false;
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
        if (string.IsNullOrWhiteSpace(_query) && _settings.Search.ShowTrendingWhenEmpty &&
            Mode == GifSearchMode.Trending && (IsBusy || HasResults)) return;
        CancellationTokenSource? previous = _operationCancellation;
        _operationCancellation = null;
        previous?.Cancel();
        CancelSuggestionOperation();
        SetProperty(ref _query, string.Empty, nameof(Query));
        OnPropertyChanged(nameof(CanSubmitQuery));
        _activeQuery = null;
        _continuationToken = null;
        Suggestions.Clear();
        ClearResults();
        Mode = GifSearchMode.None;
        OperationState = AsyncOperationState.Idle;
        Message = null;
        NotifyPaginationState();
        NotifyCommandStates();
        if (_settings.Search.ShowTrendingWhenEmpty) _emptyQueryOperation = RestoreEmptyQueryAsync();
    }

    private async Task RestoreEmptyQueryAsync()
    {
        // TrendingAsync owns request cancellation and reports failures in the view model.
        await TrendingAsync(CancellationToken.None);
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
            if (ActiveProviderId != "giphy" && !_resultIdentities.Add(
                    item.Identity))
            {
                continue;
            }

            GifCardViewModel card = new(
                    item,
                    _copyCoordinator,
                    _libraryCoordinator,
                    _previewCoordinator,
                    favoriteIdentities.Contains(
                        item.Identity),
                    searchQuery,
                    ReducedMotion);
            card.DisplayQuality = DisplayQuality;
            card.PropertyChanged += OnCardPropertyChanged;
            Results.Add(card);
        }

        _continuationToken = page.ContinuationToken;
        if (Mode == GifSearchMode.Trending && ActiveProviderId == "klipy")
            _trendingSnapshot = new GifSearchPage
            {
                Items = Results.Select(card => card.Item).ToArray(),
                ContinuationToken = _continuationToken,
                TotalCount = page.TotalCount
            };

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
            card.PropertyChanged -= OnCardPropertyChanged;
            card.StopPreviewCommand
                .Execute(null);
        }

        Results.Clear();
        _resultIdentities.Clear();
    }

    private void OnCardPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(GifCardViewModel.Message) &&
            sender is GifCardViewModel { Message: { } message })
            Message = message;
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
