using CopyGIF.Application.Providers;
using CopyGIF.Application.Search;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Search;

[TestClass]
public sealed class GifSearchCoordinatorTests
{
    [TestMethod]
    public async Task SearchAsync_UsesConfiguredPageSizeAndQuery()
    {
        FakeGifProvider provider =
            new();

        FakeSettingsStore settingsStore =
            new()
            {
                Value =
                    CreateSettings(
                        resultsPerSearch: 12)
            };

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                settingsStore);

        await coordinator.SearchAsync(
            "  cats  ");

        GifSearchRequest request =
            GetLastRequest(
                provider);

        Assert.AreEqual(
            "cats",
            request.Query);

        Assert.AreEqual(
            GifSearchKind.Search,
            request.Kind);

        Assert.AreEqual(
            12,
            request.PageSize);

        Assert.IsNull(
            request.ContinuationToken);
    }

    [TestMethod]
    public async Task SearchAsync_DoesNotDelayImmediateSearch()
    {
        FakeClock clock =
            new();

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                new FakeGifProvider(),
                new FakeSettingsStore(),
                clock: clock);

        await coordinator.SearchAsync(
            "cats");

        Assert.IsEmpty(
            clock.DelayRequests);
    }

    [TestMethod]
    public async Task SearchDebouncedAsync_UsesConfiguredDelay()
    {
        FakeClock clock =
            new();

        FakeSettingsStore settingsStore =
            new()
            {
                Value =
                    CreateSettings(
                        debounceMilliseconds: 450)
            };

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                new FakeGifProvider(),
                settingsStore,
                clock: clock);

        await coordinator.SearchDebouncedAsync(
            "cats");

        Assert.HasCount(
            1,
            clock.DelayRequests);

        Assert.AreEqual(
            TimeSpan.FromMilliseconds(
                450),
            clock.DelayRequests[0]);
    }

    [TestMethod]
    public async Task SearchDebouncedAsync_CancelsPreviousSearch()
    {
        TaskCompletionSource<bool> firstDelayStarted =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        int delayCallCount = 0;

        FakeClock clock =
            new()
            {
                DelayHandler =
                    async (_, cancellationToken) =>
                    {
                        int callNumber =
                            Interlocked.Increment(
                                ref delayCallCount);

                        if (callNumber == 1)
                        {
                            firstDelayStarted.TrySetResult(
                                true);

                            await Task.Delay(
                                    Timeout.InfiniteTimeSpan,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
            };

        FakeGifProvider provider =
            new();

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeSettingsStore(),
                clock: clock);

        Task<GifSearchPage> firstSearch =
            coordinator.SearchDebouncedAsync(
                "cats");

        await firstDelayStarted.Task;

        GifSearchPage secondPage =
            await coordinator.SearchDebouncedAsync(
                "dogs");

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
            {
                await firstSearch;
            });

        Assert.IsNotNull(
            secondPage);

        Assert.HasCount(
            1,
            provider.SearchRequests);

        Assert.AreEqual(
            "dogs",
            provider.SearchRequests[0].Query);
    }

    [TestMethod]
    public async Task SearchAsync_PropagatesCancellationToProvider()
    {
        bool providerObservedCancellation =
            false;

        TaskCompletionSource<bool> providerStarted =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        FakeGifProvider provider =
            new()
            {
                SearchHandler =
                    async (_, cancellationToken) =>
                    {
                        providerStarted.TrySetResult(
                            true);

                        try
                        {
                            await Task.Delay(
                                    Timeout.InfiniteTimeSpan,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                            when (cancellationToken.IsCancellationRequested)
                        {
                            providerObservedCancellation =
                                true;

                            throw;
                        }

                        return GifSearchPage.Empty();
                    }
            };

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeSettingsStore());

        using CancellationTokenSource cancellation =
            new();

        Task<GifSearchPage> search =
            coordinator.SearchAsync(
                "cats",
                cancellation.Token);

        await providerStarted.Task;

        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () =>
            {
                await search;
            });

        Assert.IsTrue(
            providerObservedCancellation);
    }

    [TestMethod]
    public async Task TrendingAsync_CreatesTrendingRequest()
    {
        FakeGifProvider provider =
            new();

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeSettingsStore());

        await coordinator.TrendingAsync();

        GifSearchRequest request =
            GetLastRequest(
                provider);

        Assert.AreEqual(
            GifSearchKind.Trending,
            request.Kind);

        Assert.AreEqual(
            string.Empty,
            request.Query);

        Assert.IsNull(
            request.ContinuationToken);
    }

    [TestMethod]
    public async Task TrendingAsync_WhenDisabled_DoesNotCallProvider()
    {
        FakeGifProvider provider =
            new();

        FakeSettingsStore settingsStore =
            new()
            {
                Value =
                    CreateSettings(
                        showTrendingWhenEmpty: false)
            };

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                settingsStore);

        GifSearchPage page =
            await coordinator.TrendingAsync();

        Assert.IsEmpty(
            page.Items);

        Assert.IsEmpty(
            provider.SearchRequests);
    }

    [TestMethod]
    public async Task LoadMoreAsync_PassesContinuationToken()
    {
        FakeGifProvider provider =
            new();

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeSettingsStore());

        await coordinator.LoadMoreAsync(
            "cats",
            "3");

        GifSearchRequest request =
            GetLastRequest(
                provider);

        Assert.AreEqual(
            GifSearchKind.Search,
            request.Kind);

        Assert.AreEqual(
            "3",
            request.ContinuationToken);
    }

    [TestMethod]
    public async Task LoadMoreTrendingAsync_PassesContinuationToken()
    {
        FakeGifProvider provider =
            new();

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeSettingsStore());

        await coordinator.LoadMoreTrendingAsync(
            "4");

        GifSearchRequest request =
            GetLastRequest(
                provider);

        Assert.AreEqual(
            GifSearchKind.Trending,
            request.Kind);

        Assert.AreEqual(
            "4",
            request.ContinuationToken);
    }

    [TestMethod]
    public async Task LoadMoreAsync_SerializesPaginationRequests()
    {
        TaskCompletionSource<bool> firstRequestStarted =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        TaskCompletionSource<bool> releaseFirstRequest =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        int requestNumber = 0;

        FakeGifProvider provider =
            new()
            {
                SearchHandler =
                    async (_, cancellationToken) =>
                    {
                        int currentRequest =
                            Interlocked.Increment(
                                ref requestNumber);

                        if (currentRequest == 1)
                        {
                            firstRequestStarted.TrySetResult(
                                true);

                            await releaseFirstRequest.Task
                                .WaitAsync(
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }

                        return GifSearchPage.Empty();
                    }
            };

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeSettingsStore());

        Task<GifSearchPage> firstRequest =
            coordinator.LoadMoreAsync(
                "cats",
                "page-2");

        await firstRequestStarted.Task;

        Task<GifSearchPage> secondRequest =
            coordinator.LoadMoreAsync(
                "cats",
                "page-3");

        Assert.HasCount(
            1,
            provider.SearchRequests);

        releaseFirstRequest.TrySetResult(
            true);

        await Task.WhenAll(
            firstRequest,
            secondRequest);

        Assert.HasCount(
            2,
            provider.SearchRequests);
    }

    [TestMethod]
    public async Task SearchAsync_NormalizesInvalidPageSize()
    {
        FakeGifProvider provider =
            new();

        FakeSettingsStore settingsStore =
            new()
            {
                Value =
                    CreateSettings(
                        resultsPerSearch: 500)
            };

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                settingsStore);

        await coordinator.SearchAsync(
            "cats");

        Assert.AreEqual(
            24,
            GetLastRequest(
                provider)
                .PageSize);
    }

    [TestMethod]
    public async Task SearchAsync_PassesSettingsToProviderAccessor()
    {
        FakeGifProvider provider =
            new();

        AppSettings configuredSettings =
            CreateSettings(
                activeProviderId: "future");

        FakeProviderAccessor accessor =
            new(
                provider);

        FakeSettingsStore settingsStore =
            new()
            {
                Value = configuredSettings
            };

        FakeClock clock =
            new();

        FakeSearchSuggestionCoordinator suggestions =
            new();

        using GifSearchCoordinator coordinator =
            new(
                accessor,
                settingsStore,
                suggestions,
                clock);

        await coordinator.SearchAsync(
            "cats");

        Assert.AreEqual(
            1,
            accessor.CallCount);

        Assert.AreEqual(
            "future",
            accessor.LastSettings!
                .Providers
                .ActiveProviderId);
    }

    [TestMethod]
    public async Task SearchAsync_AfterProviderSuccess_RecordsHistory()
    {
        FakeSearchSuggestionCoordinator suggestions =
            new();

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                new FakeGifProvider(),
                new FakeSettingsStore(),
                suggestions: suggestions);

        await coordinator.SearchAsync(
            "  Cats  ");

        Assert.HasCount(
            1,
            suggestions.RecordedQueries);

        Assert.AreEqual(
            "Cats",
            suggestions.RecordedQueries[0]);
    }

    [TestMethod]
    public async Task SearchAsync_WhenProviderFails_DoesNotRecordHistory()
    {
        FakeGifProvider provider =
            new()
            {
                SearchHandler =
                    (_, _) =>
                        Task.FromException<GifSearchPage>(
                            new InvalidOperationException(
                                "Provider failed."))
            };

        FakeSearchSuggestionCoordinator suggestions =
            new();

        using GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeSettingsStore(),
                suggestions: suggestions);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () =>
            {
                await coordinator.SearchAsync(
                    "cats");
            });

        Assert.IsEmpty(
            suggestions.RecordedQueries);
    }

    private static GifSearchCoordinator
        CreateCoordinator(
            IGifProvider provider,
            FakeSettingsStore settingsStore,
            FakeClock? clock = null,
            FakeSearchSuggestionCoordinator? suggestions = null)
    {
        FakeClock effectiveClock =
            clock ?? new FakeClock();

        return new GifSearchCoordinator(
            new FakeProviderAccessor(
                provider),
            settingsStore,
            suggestions ??
                new FakeSearchSuggestionCoordinator(),
            effectiveClock);
    }

    private static GifSearchRequest GetLastRequest(
        FakeGifProvider provider)
    {
        IReadOnlyList<GifSearchRequest> requests =
            provider.SearchRequests;

        return requests.Count > 0
            ? requests[^1]
            :
            throw new AssertFailedException(
                "Provider did not receive a request.");
    }

    private static AppSettings CreateSettings(
        int resultsPerSearch = 24,
        int debounceMilliseconds = 300,
        bool showTrendingWhenEmpty = true,
        string activeProviderId = "klipy")
    {
        return new AppSettings
        {
            Search =
                new SearchSettings
                {
                    ResultsPerSearch =
                        resultsPerSearch,

                    DebounceMilliseconds =
                        debounceMilliseconds,

                    ShowTrendingWhenEmpty =
                        showTrendingWhenEmpty
                },

            Providers =
                new ProviderSettings
                {
                    ActiveProviderId =
                        activeProviderId
                }
        };
    }

    private sealed class FakeProviderAccessor :
        IActiveGifProviderAccessor
    {
        private readonly IGifProvider
            _provider;

        public FakeProviderAccessor(
            IGifProvider provider)
        {
            _provider = provider;
        }

        public int CallCount
        {
            get;
            private set;
        }

        public AppSettings? LastSettings
        {
            get;
            private set;
        }

        public IGifProvider GetActiveProvider(
            AppSettings settings)
        {
            CallCount++;
            LastSettings = settings;

            return _provider;
        }
    }

    private sealed class FakeSearchSuggestionCoordinator :
        ISearchSuggestionCoordinator
    {
        private readonly List<string>
            _recordedQueries = [];

        public IReadOnlyList<string> RecordedQueries =>
            _recordedQueries.ToArray();

        public Task<IReadOnlyList<string>>
            GetSuggestionsAsync(
                string input,
                int maximumResults = 8,
                CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<string> result = [];

            return Task.FromResult(
                result);
        }

        public Task RecordSearchAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _recordedQueries.Add(
                query);

            return Task.CompletedTask;
        }

        public Task ClearHistoryAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _recordedQueries.Clear();

            return Task.CompletedTask;
        }
    }
}
