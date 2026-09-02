using CopyGIF.Application.Providers;
using CopyGIF.Application.Search;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Tests.Search;

[TestClass]
public sealed class GifSearchCoordinatorTests
{
    [TestMethod]
    public async Task SearchAsync_UsesConfiguredPageSizeAndQuery()
    {
        FakeGifProvider provider =
            new();

        FakeSettingsStore settings =
            new(
                CreateSettings(
                    resultsPerSearch: 12));

        GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                settings);

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
    public async Task TrendingAsync_CreatesTrendingRequest()
    {
        FakeGifProvider provider =
            new();

        GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeSettingsStore(
                    new AppSettings()));

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
    public async Task LoadMoreAsync_PassesContinuationToken()
    {
        FakeGifProvider provider =
            new();

        GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeSettingsStore(
                    new AppSettings()));

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

        GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                new FakeSettingsStore(
                    new AppSettings()));

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
    public async Task SearchAsync_NormalizesInvalidPageSize()
    {
        FakeGifProvider provider =
            new();

        FakeSettingsStore settings =
            new(
                CreateSettings(
                    resultsPerSearch: 500));

        GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                settings);

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
                activeProviderId:
                    "future");

        FakeProviderAccessor accessor =
            new(
                provider);

        GifSearchCoordinator coordinator =
            new(
                accessor,
                new FakeSettingsStore(
                    configuredSettings));

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

    private static GifSearchCoordinator
        CreateCoordinator(
            IGifProvider provider,
            ISettingsStore settingsStore)
    {
        return new GifSearchCoordinator(
            new FakeProviderAccessor(
                provider),
            settingsStore);
    }

    private static GifSearchRequest GetLastRequest(
        FakeGifProvider provider)
    {
        return provider.LastRequest ??
            throw new AssertFailedException(
                "Provider did not receive a request.");
    }

    private static AppSettings CreateSettings(
        int resultsPerSearch = 24,
        string activeProviderId = "klipy")
    {
        return new AppSettings
        {
            Search =
                new SearchSettings
                {
                    ResultsPerSearch =
                        resultsPerSearch
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

    private sealed class FakeSettingsStore :
        ISettingsStore
    {
        private AppSettings _settings;

        public FakeSettingsStore(
            AppSettings settings)
        {
            _settings = settings;
        }

        public Task<AppSettings> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _settings);
        }

        public Task SaveAsync(
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            _settings = settings;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeGifProvider :
        IGifProvider
    {
        public string Id => "test";

        public string DisplayName =>
            "Test Provider";

        public GifSearchRequest? LastRequest
        {
            get;
            private set;
        }

        public Task<GifSearchPage> SearchAsync(
            GifSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastRequest = request;

            return Task.FromResult(
                GifSearchPage.Empty());
        }

        public Task<CredentialValidationResult>
            ValidateCredentialAsync(
                string credential,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                CredentialValidationResult.Valid());
        }

        public Task RegisterShareAsync(
            string itemId,
            string? searchQuery,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
