using CopyGIF.Application.Search;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.ViewModels;

namespace CopyGIF.Presentation.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public void SearchCommand_IsDisabledForEmptyQuery()
    {
        FakeGifSearchCoordinator coordinator =
            new();

        MainViewModel viewModel =
            new(coordinator);

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
        FakeGifSearchCoordinator coordinator =
            new()
            {
                SearchResult =
                    new GifSearchPage
                    {
                        Items =
                        [
                            CreateGif("1"),
                            CreateGif("2")
                        ],

                        ContinuationToken =
                            "next-page"
                    }
            };

        MainViewModel viewModel =
            new(coordinator)
            {
                Query = "cats"
            };

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        Assert.HasCount(
            2,
            viewModel.Results);

        Assert.AreEqual(
            "2 results",
            viewModel.StatusMessage);

        Assert.IsTrue(
            viewModel.HasResults);

        Assert.IsTrue(
            viewModel.HasMoreResults);

        Assert.AreEqual(
            "cats",
            coordinator.LastQuery);
    }

    [TestMethod]
    public async Task SearchCommand_ShowsMissingCredentialMessage()
    {
        FakeGifSearchCoordinator coordinator =
            new()
            {
                SearchException =
                    new GifProviderException(
                        "test",
                        GifProviderFailure
                            .MissingCredential,
                        "Credential missing.")
            };

        MainViewModel viewModel =
            new(coordinator)
            {
                Query = "cats"
            };

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            "A GIF provider API key is required.",
            viewModel.StatusMessage);

        Assert.IsEmpty(
            viewModel.Results);
    }

    private static GifItem CreateGif(
        string id)
    {
        return new GifItem
        {
            ProviderId =
                "test",

            Id =
                id,

            Title =
                $"GIF {id}",

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

    private sealed class
        FakeGifSearchCoordinator :
            IGifSearchCoordinator
    {
        public string? LastQuery
        {
            get;
            private set;
        }

        public string? LastContinuationToken
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
                Items = []
            };

        public GifProviderException?
            SearchException
        {
            get;
            init;
        }

        public Task<GifSearchPage>
            SearchAsync(
                string query,
                CancellationToken cancellationToken =
                    default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastQuery =
                query;

            if (SearchException is not null)
            {
                throw SearchException;
            }

            return Task.FromResult(
                SearchResult);
        }

        public Task<GifSearchPage>
            LoadMoreAsync(
                string query,
                string continuationToken,
                CancellationToken cancellationToken =
                    default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastQuery =
                query;

            LastContinuationToken =
                continuationToken;

            if (SearchException is not null)
            {
                throw SearchException;
            }

            return Task.FromResult(
                SearchResult);
        }
    }
}