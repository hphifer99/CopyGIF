using CopyGIF.Application.Library;
using CopyGIF.Application.Media;
using CopyGIF.Application.Search;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Library;
using CopyGIF.Presentation.Main;
using CopyGIF.Presentation.Search;

namespace CopyGIF.Presentation.Tests.Main;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public void Constructor_StartsOnSearch()
    {
        Harness harness =
            new();

        Assert.AreEqual(
            MainSection.Search,
            harness.ViewModel.SelectedSection);

        Assert.IsTrue(
            harness.ViewModel.IsSearchSelected);

        Assert.IsFalse(
            harness.ViewModel.IsFavoritesSelected);

        Assert.IsFalse(
            harness.ViewModel.IsRecentsSelected);
    }

    [TestMethod]
    public async Task ShowFavoritesCommand_SelectsAndRefreshesFavorites()
    {
        Harness harness =
            new();

        harness.Library.Snapshot =
            new LibrarySnapshot
            {
                Favorites =
                [
                    CreateEntry(
                        "1")
                ]
            };

        await harness.ViewModel
            .ShowFavoritesCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            MainSection.Favorites,
            harness.ViewModel.SelectedSection);

        Assert.HasCount(
            1,
            harness.ViewModel
                .Favorites
                .Items);

        Assert.AreEqual(
            1,
            harness.Library.LoadCount);
    }

    [TestMethod]
    public async Task ShowRecentsCommand_SelectsAndRefreshesRecents()
    {
        Harness harness =
            new();

        harness.Library.Snapshot =
            new LibrarySnapshot
            {
                Recents =
                [
                    CreateEntry(
                        "2")
                ]
            };

        await harness.ViewModel
            .ShowRecentsCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            MainSection.Recents,
            harness.ViewModel.SelectedSection);

        Assert.HasCount(
            1,
            harness.ViewModel
                .Recents
                .Items);

        Assert.AreEqual(
            1,
            harness.Library.LoadCount);
    }

    [TestMethod]
    public async Task RefreshCurrentSection_SearchWithQuery_PerformsSearch()
    {
        Harness harness =
            new();

        harness.Search.SearchResult =
            new GifSearchPage
            {
                Items =
                [
                    CreateGif(
                        "1")
                ]
            };

        harness.ViewModel
            .Search
            .Query =
                "cats";

        await harness.ViewModel
            .RefreshCurrentSectionCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            harness.Search.SearchCount);

        Assert.AreEqual(
            0,
            harness.Search.TrendingCount);

        Assert.HasCount(
            1,
            harness.ViewModel
                .Search
                .Results);
    }

    [TestMethod]
    public async Task RefreshCurrentSection_BlankQuery_LoadsTrending()
    {
        Harness harness =
            new();

        harness.Search.TrendingResult =
            new GifSearchPage
            {
                Items =
                [
                    CreateGif(
                        "10")
                ]
            };

        await harness.ViewModel
            .RefreshCurrentSectionCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            0,
            harness.Search.SearchCount);

        Assert.AreEqual(
            1,
            harness.Search.TrendingCount);

        Assert.HasCount(
            1,
            harness.ViewModel
                .Search
                .Results);
    }

    [TestMethod]
    public void ReducedMotion_PropagatesToAllMainSections()
    {
        Harness harness =
            new();

        harness.ViewModel.ReducedMotion =
            true;

        Assert.IsTrue(
            harness.ViewModel
                .Search
                .ReducedMotion);

        Assert.IsTrue(
            harness.ViewModel
                .Favorites
                .ReducedMotion);

        Assert.IsTrue(
            harness.ViewModel
                .Recents
                .ReducedMotion);
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

            PreviewUri =
                new Uri(
                    $"https://example.com/{id}-preview.gif"),

            GifUri =
                new Uri(
                    $"https://example.com/{id}.gif"),

            AddedAtUtc =
                DateTimeOffset.UtcNow
        };
    }

    private sealed class Harness
    {
        public Harness()
        {
            Search =
                new FakeSearchCoordinator();

            Suggestions =
                new FakeSuggestionCoordinator();

            Library =
                new FakeLibraryCoordinator();

            FakeCopyCoordinator copy =
                new();

            FakePreviewCoordinator preview =
                new();

            SearchViewModel searchViewModel =
                new(
                    Search,
                    Suggestions,
                    copy,
                    Library,
                    preview);

            FavoritesViewModel favorites =
                new(
                    Library,
                    copy,
                    preview);

            RecentsViewModel recents =
                new(
                    Library,
                    copy,
                    preview);

            ViewModel =
                new MainViewModel(
                    searchViewModel,
                    favorites,
                    recents);
        }

        public FakeSearchCoordinator Search
        { get; }

        public FakeSuggestionCoordinator Suggestions
        { get; }

        public FakeLibraryCoordinator Library
        { get; }

        public MainViewModel ViewModel
        { get; }
    }

    private sealed class FakeSearchCoordinator :
        IGifSearchCoordinator
    {
        public GifSearchPage SearchResult
        {
            get;
            set;
        } =
            GifSearchPage.Empty();

        public GifSearchPage TrendingResult
        {
            get;
            set;
        } =
            GifSearchPage.Empty();

        public int SearchCount
        {
            get;
            private set;
        }

        public int TrendingCount
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

            return Task.FromResult(
                SearchResult);
        }

        public Task<GifSearchPage> SearchDebouncedAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            return SearchAsync(
                query,
                cancellationToken);
        }

        public Task<GifSearchPage> TrendingAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            TrendingCount++;

            return Task.FromResult(
                TrendingResult);
        }

        public Task<GifSearchPage> LoadMoreAsync(
            string query,
            string continuationToken,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                GifSearchPage.Empty());
        }

        public Task<GifSearchPage> LoadMoreTrendingAsync(
            string continuationToken,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                GifSearchPage.Empty());
        }
    }

    private sealed class FakeSuggestionCoordinator :
        ISearchSuggestionCoordinator
    {
        public Task<IReadOnlyList<string>>
            GetSuggestionsAsync(
                string input,
                int maximumResults = 8,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult<
                IReadOnlyList<string>>(
                    []);
        }

        public Task RecordSearchAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearHistoryAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLibraryCoordinator :
        IGifLibraryCoordinator
    {
        public LibrarySnapshot Snapshot
        {
            get;
            set;
        } =
            new();

        public int LoadCount
        {
            get;
            private set;
        }

        public Task<LibrarySnapshot> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LoadCount++;

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
