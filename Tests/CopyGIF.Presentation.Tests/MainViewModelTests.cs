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
                Query =
                    "cats"
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

        Assert.IsTrue(
            viewModel.LoadMoreCommand
                .CanExecute(null));

        Assert.AreEqual(
            "cats",
            coordinator.LastQuery);
    }

    [TestMethod]
    public async Task LoadMoreCommand_AppendsResults()
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
                            "page-two"
                    },

                LoadMoreResult =
                    new GifSearchPage
                    {
                        Items =
                        [
                            CreateGif("3"),
                            CreateGif("4")
                        ],

                        ContinuationToken =
                            null
                    }
            };

        MainViewModel viewModel =
            new(coordinator)
            {
                Query =
                    "cats"
            };

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        await viewModel
            .LoadMoreCommand
            .ExecuteAsync(null);

        Assert.HasCount(
            4,
            viewModel.Results);

        Assert.AreEqual(
            "page-two",
            coordinator.LastContinuationToken);

        Assert.IsFalse(
            viewModel.HasMoreResults);

        Assert.IsFalse(
            viewModel.LoadMoreCommand
                .CanExecute(null));

        Assert.AreEqual(
            "4 results - end of results",
            viewModel.StatusMessage);
    }

    [TestMethod]
    public async Task LoadMoreCommand_DoesNotAddDuplicates()
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
                            "page-two"
                    },

                LoadMoreResult =
                    new GifSearchPage
                    {
                        Items =
                        [
                            CreateGif("2"),
                            CreateGif("3")
                        ],

                        ContinuationToken =
                            null
                    }
            };

        MainViewModel viewModel =
            new(coordinator)
            {
                Query =
                    "cats"
            };

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        await viewModel
            .LoadMoreCommand
            .ExecuteAsync(null);

        Assert.HasCount(
            3,
            viewModel.Results);

        CollectionAssert.AreEqual(
            new[]
            {
                "1",
                "2",
                "3"
            },
            viewModel.Results
                .Select(
                    item => item.Id)
                .ToArray());
    }

    [TestMethod]
    public async Task LoadMoreCommand_IsDisabledWhenQueryChanges()
    {
        FakeGifSearchCoordinator coordinator =
            new()
            {
                SearchResult =
                    new GifSearchPage
                    {
                        Items =
                        [
                            CreateGif("1")
                        ],

                        ContinuationToken =
                            "page-two"
                    }
            };

        MainViewModel viewModel =
            new(coordinator)
            {
                Query =
                    "cats"
            };

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.LoadMoreCommand
                .CanExecute(null));

        viewModel.Query =
            "dogs";

        Assert.IsFalse(
            viewModel.LoadMoreCommand
                .CanExecute(null));
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
                Query =
                    "cats"
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

        public GifSearchPage LoadMoreResult
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
                LoadMoreResult);
        }
    }
}