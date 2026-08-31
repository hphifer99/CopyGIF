using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.ViewModels;

namespace CopyGIF.Presentation.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public void SearchCommand_IsDisabledForEmptyQuery()
    {
        FakeGifProvider provider =
            new();

        FakeSettingsStore settings =
            new(new AppSettings());

        MainViewModel viewModel =
            new(
                provider,
                settings);

        Assert.IsFalse(
            viewModel.SearchCommand
                .CanExecute(null));

        viewModel.Query =
            "cats";

        Assert.IsTrue(
            viewModel.SearchCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task SearchCommand_LoadsResults()
    {
        FakeGifProvider provider =
            new()
            {
                SearchResult =
                    new GifSearchPage
                    {
                        Items =
                            new[]
                            {
                                CreateGif("1"),
                                CreateGif("2")
                            },

                        ContinuationToken =
                            "next-page"
                    }
            };

        FakeSettingsStore settings =
            new(
                new AppSettings
                {
                    Search =
                        new SearchSettings
                        {
                            ResultsPerSearch = 12
                        }
                });

        MainViewModel viewModel =
            new(
                provider,
                settings)
            {
                Query = "cats"
            };

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            2,
            viewModel.Results.Count);

        Assert.AreEqual(
            "2 results",
            viewModel.StatusMessage);

        Assert.IsTrue(
            viewModel.HasResults);

        Assert.IsTrue(
            viewModel.HasMoreResults);

        GifSearchRequest request =
            provider.LastRequest ??
            throw new AssertFailedException(
                "The provider did not receive a search request.");

        Assert.AreEqual(
            "cats",
            request.Query);

        Assert.AreEqual(
            12,
            request.PageSize);
    }

    [TestMethod]
    public async Task SearchCommand_ShowsMissingCredentialMessage()
    {
        FakeGifProvider provider =
            new()
            {
                SearchException =
                    new GifProviderException(
                        "klipy",
                        GifProviderFailure.MissingCredential,
                        "Credential missing.")
            };

        FakeSettingsStore settings =
            new(new AppSettings());

        MainViewModel viewModel =
            new(
                provider,
                settings)
            {
                Query = "cats"
            };

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            "KLIPY API key required.",
            viewModel.StatusMessage);

        Assert.AreEqual(
            0,
            viewModel.Results.Count);
    }

    private static GifItem CreateGif(
        string id)
    {
        return new GifItem
        {
            ProviderId = "test",
            Id = id,
            Title = $"GIF {id}",

            ThumbnailUri =
                new Uri(
                    $"https://example.com/{id}-thumb.gif"),

            PreviewUri =
                new Uri(
                    $"https://example.com/{id}-preview.gif"),

            GifUri =
                new Uri(
                    $"https://example.com/{id}.gif")
        };
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
        public string Id =>
            "test";

        public string DisplayName =>
            "Test Provider";

        public GifSearchRequest? LastRequest
        {
            get;
            private set;
        }

        public GifSearchPage SearchResult
        {
            get;
            init;
        } =
            new GifSearchPage
            {
                Items =
                    Array.Empty<GifItem>()
            };

        public GifProviderException? SearchException
        {
            get;
            init;
        }

        public Task<GifSearchPage> SearchAsync(
            GifSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastRequest =
                request;

            if (SearchException is not null)
            {
                throw SearchException;
            }

            return Task.FromResult(
                SearchResult);
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