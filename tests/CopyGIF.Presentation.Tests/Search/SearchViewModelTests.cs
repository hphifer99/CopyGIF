using CopyGIF.Application.Library;
using CopyGIF.Application.Media;
using CopyGIF.Application.Search;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Search;

namespace CopyGIF.Presentation.Tests.Search;

[TestClass]
public sealed class SearchViewModelTests
{
    [TestMethod]
    public void SearchCommand_IsDisabledForEmptyQuery()
    {
        SearchViewModel viewModel =
            CreateViewModel();

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
        FakeSearchCoordinator search =
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
                            "next"
                    }
            };

        SearchViewModel viewModel =
            CreateViewModel(
                searchCoordinator:
                    search);

        viewModel.Query =
            "  cats  ";

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        Assert.HasCount(
            2,
            viewModel.Results);

        Assert.AreEqual(
            "cats",
            search.LastQuery);

        Assert.AreEqual(
            GifSearchMode.Search,
            viewModel.Mode);

        Assert.AreEqual(
            "cats",
            viewModel.ActiveQuery);

        Assert.IsTrue(
            viewModel.HasMoreResults);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);
    }

    [TestMethod]
    public async Task SearchCommand_DoesNotDuplicateApplicationHistoryRecording()
    {
        FakeSuggestionCoordinator suggestions =
            new();

        SearchViewModel viewModel =
            CreateViewModel(
                suggestionCoordinator:
                    suggestions);

        viewModel.Query =
            "cats";

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            0,
            suggestions.RecordSearchCount);
    }

    [TestMethod]
    public async Task SearchCommand_MarksExistingFavorites()
    {
        FakeLibraryCoordinator library =
            new()
            {
                Snapshot =
                    new LibrarySnapshot
                    {
                        Favorites =
                        [
                            CreateEntry("2")
                        ]
                    }
            };

        FakeSearchCoordinator search =
            new()
            {
                SearchResult =
                    new GifSearchPage
                    {
                        Items =
                        [
                            CreateGif("1"),
                            CreateGif("2")
                        ]
                    }
            };

        SearchViewModel viewModel =
            CreateViewModel(
                searchCoordinator:
                    search,
                libraryCoordinator:
                    library);

        viewModel.Query =
            "cats";

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        Assert.IsFalse(
            viewModel.Results[0]
                .IsFavorite);

        Assert.IsTrue(
            viewModel.Results[1]
                .IsFavorite);
    }

    [TestMethod]
    public async Task SearchDebouncedCommand_UsesDebouncedCoordinatorMethod()
    {
        FakeSearchCoordinator search =
            new();

        SearchViewModel viewModel =
            CreateViewModel(
                searchCoordinator:
                    search);

        viewModel.Query =
            "cats";

        await viewModel
            .SearchDebouncedCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            0,
            search.SearchCount);

        Assert.AreEqual(
            1,
            search.DebouncedSearchCount);
    }

    [TestMethod]
    public async Task TrendingCommand_LoadsTrendingResults()
    {
        FakeSearchCoordinator search =
            new()
            {
                TrendingResult =
                    new GifSearchPage
                    {
                        Items =
                        [
                            CreateGif("10")
                        ],

                        ContinuationToken =
                            "trending-next"
                    }
            };

        SearchViewModel viewModel =
            CreateViewModel(
                searchCoordinator:
                    search);

        await viewModel
            .TrendingCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            search.TrendingCount);

        Assert.HasCount(
            1,
            viewModel.Results);

        Assert.AreEqual(
            GifSearchMode.Trending,
            viewModel.Mode);

        Assert.IsNull(
            viewModel.ActiveQuery);
    }

    [TestMethod]
    public async Task LoadMoreCommand_AppendsAndDeduplicatesResults()
    {
        FakeSearchCoordinator search =
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
                        ]
                    }
            };

        SearchViewModel viewModel =
            CreateViewModel(
                searchCoordinator:
                    search);

        viewModel.Query =
            "cats";

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
                    card =>
                        card.Id)
                .ToArray());

        Assert.AreEqual(
            "page-two",
            search.LastContinuationToken);

        Assert.IsFalse(
            viewModel.HasMoreResults);
    }

    [TestMethod]
    public async Task LoadMoreCommand_UsesTrendingPagination()
    {
        FakeSearchCoordinator search =
            new()
            {
                TrendingResult =
                    new GifSearchPage
                    {
                        Items =
                        [
                            CreateGif("1")
                        ],

                        ContinuationToken =
                            "trending-two"
                    },

                TrendingLoadMoreResult =
                    new GifSearchPage
                    {
                        Items =
                        [
                            CreateGif("2")
                        ]
                    }
            };

        SearchViewModel viewModel =
            CreateViewModel(
                searchCoordinator:
                    search);

        await viewModel
            .TrendingCommand
            .ExecuteAsync(null);

        await viewModel
            .LoadMoreCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            search.TrendingLoadMoreCount);

        Assert.HasCount(
            2,
            viewModel.Results);
    }

    [TestMethod]
    public async Task QueryChange_DisablesSearchPagination()
    {
        FakeSearchCoordinator search =
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
                            "next"
                    }
            };

        SearchViewModel viewModel =
            CreateViewModel(
                searchCoordinator:
                    search);

        viewModel.Query =
            "cats";

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
    public async Task RefreshSuggestionsCommand_LoadsDistinctSuggestions()
    {
        FakeSuggestionCoordinator suggestions =
            new()
            {
                Suggestions =
                [
                    "cats",
                    " cats ",
                    "cat memes",
                    "funny cats"
                ]
            };

        SearchViewModel viewModel =
            CreateViewModel(
                suggestionCoordinator:
                    suggestions);

        viewModel.Query =
            "cat";

        await viewModel
            .RefreshSuggestionsCommand
            .ExecuteAsync(null);

        CollectionAssert.AreEqual(
            new[]
            {
                "cats",
                "cat memes",
                "funny cats"
            },
            viewModel.Suggestions
                .ToArray());

        Assert.AreEqual(
            "cat",
            suggestions.LastSuggestionInput);

        Assert.AreEqual(
            8,
            suggestions.LastMaximumResults);
    }

    [TestMethod]
    public async Task ClearSuggestionHistoryCommand_ClearsSuggestions()
    {
        FakeSuggestionCoordinator suggestions =
            new()
            {
                Suggestions =
                [
                    "cats"
                ]
            };

        SearchViewModel viewModel =
            CreateViewModel(
                suggestionCoordinator:
                    suggestions);

        viewModel.Query =
            "cat";

        await viewModel
            .RefreshSuggestionsCommand
            .ExecuteAsync(null);

        await viewModel
            .ClearSuggestionHistoryCommand
            .ExecuteAsync(null);

        Assert.IsEmpty(
            viewModel.Suggestions);

        Assert.AreEqual(
            1,
            suggestions.ClearHistoryCount);
    }

    [TestMethod]
    public async Task ClearQueryCommand_ClearsQueryResultsAndSearchState()
    {
        FakeSearchCoordinator search =
            new()
            {
                SearchResult =
                    new GifSearchPage
                    {
                        Items =
                        [
                            CreateGif("1")
                        ]
                    }
            };

        SearchViewModel viewModel =
            CreateViewModel(
                searchCoordinator:
                    search);

        viewModel.Query =
            "cats";

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.ClearQueryCommand
                .CanExecute(null));

        viewModel.ClearQueryCommand
            .Execute(null);

        Assert.AreEqual(
            string.Empty,
            viewModel.Query);

        Assert.IsEmpty(
            viewModel.Results);

        Assert.AreEqual(
            GifSearchMode.None,
            viewModel.Mode);

        Assert.AreEqual(
            AsyncOperationStatus.Idle,
            viewModel.OperationState.Status);
    }

    [TestMethod]
    public async Task MissingCredential_ProducesSpecificMessage()
    {
        FakeSearchCoordinator search =
            new()
            {
                SearchException =
                    new GifProviderException(
                        "test",
                        GifProviderFailure.MissingCredential,
                        "Missing credential.")
            };

        SearchViewModel viewModel =
            CreateViewModel(
                searchCoordinator:
                    search);

        viewModel.Query =
            "cats";

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            AsyncOperationStatus.Failed,
            viewModel.OperationState.Status);

        Assert.AreEqual(
            "missing_credential",
            viewModel.Message?.Code);

        Assert.AreEqual(
            UserMessageSeverity.Warning,
            viewModel.Message?.Severity);
    }

    [TestMethod]
    public async Task ReducedMotion_IsAppliedToResultCards()
    {
        FakeSearchCoordinator search =
            new()
            {
                SearchResult =
                    new GifSearchPage
                    {
                        Items =
                        [
                            CreateGif("1")
                        ]
                    }
            };

        SearchViewModel viewModel =
            CreateViewModel(
                searchCoordinator:
                    search);

        viewModel.ReducedMotion =
            true;

        viewModel.Query =
            "cats";

        await viewModel
            .SearchCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.Results[0]
                .ReducedMotion);

        viewModel.ReducedMotion =
            false;

        Assert.IsFalse(
            viewModel.Results[0]
                .ReducedMotion);
    }

    private static SearchViewModel CreateViewModel(
        FakeSearchCoordinator? searchCoordinator = null,
        FakeSuggestionCoordinator? suggestionCoordinator = null,
        FakeLibraryCoordinator? libraryCoordinator = null)
    {
        return new SearchViewModel(
            searchCoordinator ??
                new FakeSearchCoordinator(),
            suggestionCoordinator ??
                new FakeSuggestionCoordinator(),
            new FakeCopyCoordinator(),
            libraryCoordinator ??
                new FakeLibraryCoordinator(),
            new FakePreviewCoordinator());
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

            Description =
                $"Description {id}",

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

    private static LibraryEntry CreateEntry(
        string id)
    {
        return new LibraryEntry
        {
            Identity =
                new GifIdentity(
                    "test",
                    id),

            Title =
                $"GIF {id}",

            ThumbnailUri =
                new Uri(
                    $"https://example.com/{id}-thumb.gif"),

            GifUri =
                new Uri(
                    $"https://example.com/{id}.gif"),

            AddedAtUtc =
                DateTimeOffset.UtcNow
        };
    }

    private sealed class FakeSearchCoordinator :
        IGifSearchCoordinator
    {
        public GifSearchPage SearchResult
        {
            get;
            init;
        } =
            GifSearchPage.Empty();

        public GifSearchPage?
            DebouncedSearchResult
        {
            get;
            init;
        }

        public GifSearchPage TrendingResult
        {
            get;
            init;
        } =
            GifSearchPage.Empty();

        public GifSearchPage LoadMoreResult
        {
            get;
            init;
        } =
            GifSearchPage.Empty();

        public GifSearchPage TrendingLoadMoreResult
        {
            get;
            init;
        } =
            GifSearchPage.Empty();

        public GifProviderException? SearchException
        {
            get;
            init;
        }

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

        public int SearchCount
        {
            get;
            private set;
        }

        public int DebouncedSearchCount
        {
            get;
            private set;
        }

        public int TrendingCount
        {
            get;
            private set;
        }

        public int TrendingLoadMoreCount
        {
            get;
            private set;
        }

        public Task<GifSearchPage> SearchAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            SearchCount++;

            LastQuery =
                query;

            if (SearchException is not null)
            {
                throw SearchException;
            }

            return Task.FromResult(
                SearchResult);
        }

        public Task<GifSearchPage> SearchDebouncedAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            DebouncedSearchCount++;

            LastQuery =
                query;

            if (SearchException is not null)
            {
                throw SearchException;
            }

            return Task.FromResult(
                DebouncedSearchResult ??
                SearchResult);
        }

        public Task<GifSearchPage> TrendingAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            TrendingCount++;

            if (SearchException is not null)
            {
                throw SearchException;
            }

            return Task.FromResult(
                TrendingResult);
        }

        public Task<GifSearchPage> LoadMoreAsync(
            string query,
            string continuationToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastQuery =
                query;

            LastContinuationToken =
                continuationToken;

            return Task.FromResult(
                LoadMoreResult);
        }

        public Task<GifSearchPage> LoadMoreTrendingAsync(
            string continuationToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            TrendingLoadMoreCount++;

            LastContinuationToken =
                continuationToken;

            return Task.FromResult(
                TrendingLoadMoreResult);
        }
    }

    private sealed class FakeSuggestionCoordinator :
        ISearchSuggestionCoordinator
    {
        public IReadOnlyList<string> Suggestions
        {
            get;
            init;
        } =
            [];

        public string? LastSuggestionInput
        {
            get;
            private set;
        }

        public int LastMaximumResults
        {
            get;
            private set;
        }

        public int RecordSearchCount
        {
            get;
            private set;
        }

        public int ClearHistoryCount
        {
            get;
            private set;
        }

        public Task<IReadOnlyList<string>> GetSuggestionsAsync(
            string input,
            int maximumResults = 8,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastSuggestionInput =
                input;

            LastMaximumResults =
                maximumResults;

            return Task.FromResult(
                Suggestions);
        }

        public Task RecordSearchAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            RecordSearchCount++;

            return Task.CompletedTask;
        }

        public Task ClearHistoryAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            ClearHistoryCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeLibraryCoordinator :
        IGifLibraryCoordinator
    {
        public LibrarySnapshot Snapshot
        {
            get;
            init;
        } =
            new();

        public Task<LibrarySnapshot> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                Snapshot);
        }

        public Task<LibrarySnapshot> AddFavoriteAsync(
            GifItem item,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Snapshot);
        }

        public Task<LibrarySnapshot> RemoveFavoriteAsync(
            GifIdentity identity,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Snapshot);
        }

        public Task<LibrarySnapshot> ClearFavoritesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Snapshot);
        }

        public Task<LibrarySnapshot> RecordRecentAsync(
            GifItem item,
            DownloadedGif copiedGif,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Snapshot);
        }

        public Task<LibrarySnapshot> ClearRecentsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Snapshot);
        }
    }

    private sealed class FakeCopyCoordinator :
        IGifCopyCoordinator
    {
        public Task<DownloadedGif> CopyAsync(
            GifItem item,
            string? searchQuery,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new DownloadedGif
                {
                    Identity =
                        item.StableIdentity,

                    SourceUri =
                        item.GifUri,

                    FilePath =
                        @"C:\CopyGIF\test.gif",

                    SizeBytes =
                        1,

                    Sha256 =
                        new string(
                            'A',
                            64),

                    DownloadedAtUtc =
                        DateTimeOffset.UtcNow,

                    Purpose =
                        GifDownloadPurpose.Clipboard
                });
        }
    }

    private sealed class FakePreviewCoordinator :
        IPreviewCoordinator
    {
        public Task<Uri> GetThumbnailSourceAsync(
            GifItem item,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                item.ThumbnailUri);
        }

        public Task<Uri> GetAnimatedSourceAsync(
            GifItem item,
            bool reducedMotion,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                item.PreviewUri ??
                item.GifUri);
        }

        public Task InvalidateAsync(
            GifItem item,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CleanupAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
