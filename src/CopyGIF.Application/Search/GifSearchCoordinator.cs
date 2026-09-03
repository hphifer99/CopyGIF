using CopyGIF.Application.Providers;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Search;

public sealed class GifSearchCoordinator :
    IGifSearchCoordinator,
    IDisposable
{
    private readonly IActiveGifProviderAccessor
        _providerAccessor;

    private readonly ISettingsStore
        _settingsStore;

    private readonly ISearchSuggestionCoordinator
        _suggestionCoordinator;

    private readonly IClock
        _clock;

    private readonly object _debounceSync =
        new();

    private readonly SemaphoreSlim _paginationGate =
        new(
            initialCount: 1,
            maxCount: 1);

    private CancellationTokenSource?
        _pendingDebounceCancellation;

    private bool _disposed;

    public GifSearchCoordinator(
        IActiveGifProviderAccessor providerAccessor,
        ISettingsStore settingsStore,
        ISearchSuggestionCoordinator suggestionCoordinator,
        IClock clock)
    {
        _providerAccessor =
            providerAccessor ??
            throw new ArgumentNullException(
                nameof(providerAccessor));

        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        _suggestionCoordinator =
            suggestionCoordinator ??
            throw new ArgumentNullException(
                nameof(suggestionCoordinator));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));
    }

    public async Task<GifSearchPage> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            query);

        CancelPendingDebouncedSearch();

        AppSettings settings =
            await LoadSettingsAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return await SearchCoreAsync(
                GifSearchKind.Search,
                query,
                continuationToken: null,
                settings,
                recordSearch: true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GifSearchPage>
        SearchDebouncedAsync(
            string query,
            CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            query);

        CancellationTokenSource operationCancellation =
            BeginDebouncedSearch(
                cancellationToken);

        try
        {
            AppSettings settings =
                await LoadSettingsAsync(
                        operationCancellation.Token)
                    .ConfigureAwait(false);

            await _clock
                .DelayAsync(
                    TimeSpan.FromMilliseconds(
                        settings.Search.DebounceMilliseconds),
                    operationCancellation.Token)
                .ConfigureAwait(false);

            return await SearchCoreAsync(
                    GifSearchKind.Search,
                    query,
                    continuationToken: null,
                    settings,
                    recordSearch: true,
                    operationCancellation.Token)
                .ConfigureAwait(false);
        }
        finally
        {
            CompleteDebouncedSearch(
                operationCancellation);
        }
    }

    public async Task<GifSearchPage> TrendingAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        CancelPendingDebouncedSearch();

        AppSettings settings =
            await LoadSettingsAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (!settings.Search.ShowTrendingWhenEmpty)
        {
            return GifSearchPage.Empty();
        }

        return await SearchCoreAsync(
                GifSearchKind.Trending,
                query: string.Empty,
                continuationToken: null,
                settings,
                recordSearch: false,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<GifSearchPage> LoadMoreAsync(
        string query,
        string continuationToken,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            query);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            continuationToken);

        return LoadMoreCoreAsync(
            GifSearchKind.Search,
            query,
            continuationToken,
            cancellationToken);
    }

    public Task<GifSearchPage>
        LoadMoreTrendingAsync(
            string continuationToken,
            CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            continuationToken);

        return LoadMoreCoreAsync(
            GifSearchKind.Trending,
            query: string.Empty,
            continuationToken,
            cancellationToken);
    }

    public void Dispose()
    {
        CancellationTokenSource? pendingCancellation;

        lock (_debounceSync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            pendingCancellation =
                _pendingDebounceCancellation;

            _pendingDebounceCancellation =
                null;
        }

        pendingCancellation?.Cancel();
        _paginationGate.Dispose();
    }

    private async Task<GifSearchPage>
        LoadMoreCoreAsync(
            GifSearchKind kind,
            string query,
            string continuationToken,
            CancellationToken cancellationToken)
    {
        await _paginationGate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            AppSettings settings =
                await LoadSettingsAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            if (kind == GifSearchKind.Trending &&
                !settings.Search.ShowTrendingWhenEmpty)
            {
                return GifSearchPage.Empty();
            }

            return await SearchCoreAsync(
                    kind,
                    query,
                    continuationToken,
                    settings,
                    recordSearch: false,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _paginationGate.Release();
        }
    }

    private async Task<GifSearchPage>
        SearchCoreAsync(
            GifSearchKind kind,
            string query,
            string? continuationToken,
            AppSettings settings,
            bool recordSearch,
            CancellationToken cancellationToken)
    {
        IGifProvider provider =
            _providerAccessor
                .GetActiveProvider(
                    settings);

        string normalizedQuery =
            kind == GifSearchKind.Search
                ? query.Trim()
                : string.Empty;

        GifSearchPage page =
            await provider
                .SearchAsync(
                    new GifSearchRequest
                    {
                        Query = normalizedQuery,
                        Kind = kind,
                        PageSize =
                            settings.Search.ResultsPerSearch,
                        ContinuationToken =
                            continuationToken
                    },
                    cancellationToken)
                .ConfigureAwait(false);

        if (recordSearch)
        {
            await _suggestionCoordinator
                .RecordSearchAsync(
                    normalizedQuery,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return page;
    }

    private async Task<AppSettings> LoadSettingsAsync(
        CancellationToken cancellationToken)
    {
        return AppSettingsNormalizer.Normalize(
            await _settingsStore
                .LoadAsync(
                    cancellationToken)
                .ConfigureAwait(false));
    }

    private CancellationTokenSource BeginDebouncedSearch(
        CancellationToken cancellationToken)
    {
        CancellationTokenSource operationCancellation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        CancellationTokenSource? previousCancellation;

        try
        {
            lock (_debounceSync)
            {
                ThrowIfDisposed();

                previousCancellation =
                    _pendingDebounceCancellation;

                _pendingDebounceCancellation =
                    operationCancellation;
            }
        }
        catch
        {
            operationCancellation.Dispose();
            throw;
        }

        previousCancellation?.Cancel();

        return operationCancellation;
    }

    private void CompleteDebouncedSearch(
        CancellationTokenSource operationCancellation)
    {
        lock (_debounceSync)
        {
            if (ReferenceEquals(
                    _pendingDebounceCancellation,
                    operationCancellation))
            {
                _pendingDebounceCancellation =
                    null;
            }
        }

        operationCancellation.Dispose();
    }

    private void CancelPendingDebouncedSearch()
    {
        CancellationTokenSource? pendingCancellation;

        lock (_debounceSync)
        {
            pendingCancellation =
                _pendingDebounceCancellation;

            _pendingDebounceCancellation =
                null;
        }

        pendingCancellation?.Cancel();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}
