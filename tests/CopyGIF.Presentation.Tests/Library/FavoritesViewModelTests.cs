using CopyGIF.Application.Library;
using CopyGIF.Application.Media;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Library;

namespace CopyGIF.Presentation.Tests.Library;

[TestClass]
public sealed class FavoritesViewModelTests
{
    [TestMethod]
    public async Task LoadCommand_LoadsFavorites()
    {
        FakeLibraryCoordinator library =
            new()
            {
                Snapshot =
                    new LibrarySnapshot
                    {
                        Favorites =
                        [
                            CreateEntry("1"),
                            CreateEntry("2")
                        ]
                    }
            };

        FavoritesViewModel viewModel =
            CreateViewModel(
                library);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.HasCount(
            2,
            viewModel.Items);

        Assert.AreEqual(
            2,
            viewModel.Count);

        Assert.IsTrue(
            viewModel.HasItems);

        Assert.IsTrue(
            viewModel.Items.All(
                item =>
                    item.IsFavorite));

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);

        Assert.IsTrue(
            viewModel.ClearCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task LoadCommand_EmptySnapshot_ShowsEmptyState()
    {
        FavoritesViewModel viewModel =
            CreateViewModel(
                new FakeLibraryCoordinator());

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsEmpty(
            viewModel.Items);

        Assert.IsFalse(
            viewModel.HasItems);

        Assert.IsNotNull(
            viewModel.Message);

        Assert.AreEqual(
            UserMessageSeverity.Information,
            viewModel.Message.Severity);

        Assert.IsFalse(
            viewModel.ClearCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task ClearCommand_ClearsFavorites()
    {
        FakeLibraryCoordinator library =
            new()
            {
                Snapshot =
                    new LibrarySnapshot
                    {
                        Favorites =
                        [
                            CreateEntry("1")
                        ]
                    }
            };

        FavoritesViewModel viewModel =
            CreateViewModel(
                library);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        await viewModel
            .ClearCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            library.ClearFavoritesCount);

        Assert.IsEmpty(
            viewModel.Items);

        Assert.IsFalse(
            viewModel.HasItems);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);

        Assert.IsNotNull(
            viewModel.Message);

        Assert.AreEqual(
            "Favorites cleared.",
            viewModel.Message.Text);
    }

    [TestMethod]
    public async Task RemovingFavoriteThroughCard_RemovesCardFromCollection()
    {
        FakeLibraryCoordinator library =
            new()
            {
                Snapshot =
                    new LibrarySnapshot
                    {
                        Favorites =
                        [
                            CreateEntry("1")
                        ]
                    }
            };

        FavoritesViewModel viewModel =
            CreateViewModel(
                library);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.HasCount(
            1,
            viewModel.Items);

        await viewModel
            .Items[0]
            .ToggleFavoriteCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            library.RemoveFavoriteCount);

        Assert.IsEmpty(
            viewModel.Items);
    }

    [TestMethod]
    public async Task ReducedMotion_IsPropagatedToCards()
    {
        FakeLibraryCoordinator library =
            new()
            {
                Snapshot =
                    new LibrarySnapshot
                    {
                        Favorites =
                        [
                            CreateEntry("1")
                        ]
                    }
            };

        FavoritesViewModel viewModel =
            CreateViewModel(
                library);

        viewModel.ReducedMotion =
            true;

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.Items[0]
                .ReducedMotion);

        viewModel.ReducedMotion =
            false;

        Assert.IsFalse(
            viewModel.Items[0]
                .ReducedMotion);
    }

    private static FavoritesViewModel CreateViewModel(
        FakeLibraryCoordinator library)
    {
        return new FavoritesViewModel(
            library,
            new FakeCopyCoordinator(),
            new FakePreviewCoordinator());
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
                    $"https://example.com/{id}.gif"),

            AddedAtUtc =
                DateTimeOffset.UtcNow
        };
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

        public int RemoveFavoriteCount
        {
            get;
            private set;
        }

        public int ClearFavoritesCount
        {
            get;
            private set;
        }

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
            cancellationToken
                .ThrowIfCancellationRequested();

            RemoveFavoriteCount++;

            return Task.FromResult(
                Snapshot);
        }

        public Task<LibrarySnapshot> ClearFavoritesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            ClearFavoritesCount++;

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
                        @"C:\CopyGIF\favorite.gif",

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
