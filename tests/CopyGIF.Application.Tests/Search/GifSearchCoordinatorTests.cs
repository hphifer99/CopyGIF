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
    public async Task SearchAsync_UsesConfiguredPageSize()
    {
        FakeGifProvider provider =
            new();

        FakeSettingsStore settings =
            new(
                new AppSettings
                {
                    Search =
                        new SearchSettings
                        {
                            ResultsPerSearch =
                                12
                        }
                });

        GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                settings);

        await coordinator.SearchAsync(
            "  cats  ");

        GifSearchRequest request =
            provider.LastRequest ??
            throw new AssertFailedException(
                "Provider did not receive a request.");

        Assert.AreEqual(
            "cats",
            request.Query);

        Assert.AreEqual(
            12,
            request.PageSize);

        Assert.IsNull(
            request.ContinuationToken);
    }

    [TestMethod]
    public async Task LoadMoreAsync_PassesContinuationToken()
    {
        FakeGifProvider provider =
            new();

        FakeSettingsStore settings =
            new(
                new AppSettings());

        GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                settings);

        await coordinator.LoadMoreAsync(
            "cats",
            "page-two");

        GifSearchRequest request =
            provider.LastRequest ??
            throw new AssertFailedException(
                "Provider did not receive a request.");

        Assert.AreEqual(
            "cats",
            request.Query);

        Assert.AreEqual(
            "page-two",
            request.ContinuationToken);
    }

    [TestMethod]
    public async Task SearchAsync_ClampsPageSizeToProviderMaximum()
    {
        FakeGifProvider provider =
            new();

        FakeSettingsStore settings =
            new(
                new AppSettings
                {
                    Search =
                        new SearchSettings
                        {
                            ResultsPerSearch =
                                500
                        }
                });

        GifSearchCoordinator coordinator =
            CreateCoordinator(
                provider,
                settings);

        await coordinator.SearchAsync(
            "cats");

        GifSearchRequest request =
            provider.LastRequest ??
            throw new AssertFailedException(
                "Provider did not receive a request.");

        Assert.AreEqual(
            50,
            request.PageSize);
    }

    [TestMethod]
    public async Task SearchAsync_UsesActiveProvider()
    {
        FakeGifProvider provider =
            new();

        FakeSettingsStore settings =
            new(
                new AppSettings());

        FakeProviderAccessor accessor =
            new(
                provider);

        GifSearchCoordinator coordinator =
            new(
                accessor,
                settings);

        await coordinator.SearchAsync(
            "cats");

        Assert.AreEqual(
            1,
            accessor.GetActiveProviderCallCount);

        Assert.IsNotNull(
            provider.LastRequest);
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

    private sealed class FakeProviderAccessor :
        IActiveGifProviderAccessor
    {
        private readonly IGifProvider
            _provider;

        public FakeProviderAccessor(
            IGifProvider provider)
        {
            _provider =
                provider;
        }

        public int GetActiveProviderCallCount
        {
            get;
            private set;
        }

        public IGifProvider GetActiveProvider()
        {
            GetActiveProviderCallCount++;

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
            _settings =
                settings;
        }

        public Task<AppSettings> LoadAsync(
            CancellationToken cancellationToken =
                default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                _settings);
        }

        public Task SaveAsync(
            AppSettings settings,
            CancellationToken cancellationToken =
                default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            _settings =
                settings;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeGifProvider :
        IGifProvider
    {
        public string Id =>
            "test";

        public string DisplayName =>
            "Test Provider";

        public GifSearchRequest? LastRequest
        {
            get;
            private set;
        }

        public Task<GifSearchPage>
            SearchAsync(
                GifSearchRequest request,
                CancellationToken cancellationToken =
                    default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastRequest =
                request;

            return Task.FromResult(
                new GifSearchPage
                {
                    Items = []
                });
        }

        public Task<CredentialValidationResult>
            ValidateCredentialAsync(
                string credential,
                CancellationToken cancellationToken =
                    default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                CredentialValidationResult.Valid());
        }

        public Task RegisterShareAsync(
            string itemId,
            string? searchQuery,
            CancellationToken cancellationToken =
                default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }
}