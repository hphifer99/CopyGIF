using CopyGIF.Application.Library;
using CopyGIF.Application.Media;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Library;

namespace CopyGIF.Presentation.Tests.Library;

[TestClass]
public sealed class RecentsViewModelTests
{
    [TestMethod]
    public async Task LoadCommand_LoadsRecents()
    {
        FakeLibraryCoordinator library =
            new()
            {
                Snapshot =
                    new LibrarySnapshot
                    {
                        Recents =
                        [
                            CreateEntry("1"),
                            CreateEntry("2")
                        ]
                    }
            };

        RecentsViewModel viewModel =
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

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);

        Assert.IsTrue(
            viewModel.ClearCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task LoadCommand_MarksRecentThatIsAlsoFavorite()
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
                        ],

                        Recents =
                        [
                            CreateEntry("1"),
                            CreateEntry("2")
                        ]
                    }
            };

        RecentsViewModel viewModel =
            CreateViewModel(
                library);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsFalse(
            viewModel.Items[0]
                .IsFavorite);

        Assert.IsTrue(
            viewModel.Items[1]
                .IsFavorite);
    }

    [TestMethod]
    public async Task LoadCommand_EmptySnapshot_ShowsEmptyState()
    {
        RecentsViewModel viewModel =
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
            "Copied GIFs will appear here.",
            viewModel.Message.Text);

        Assert.IsFalse(
            viewModel.ClearCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task ClearCommand_ClearsRecents()
    {
        FakeLibraryCoordinator library =
            new()
            {
                Snapshot =
                    new LibrarySnapshot
                    {
                        Recents =
                        [
                            CreateEntry("1")
                        ]
                    }
            };

        RecentsViewModel viewModel =
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
            library.ClearRecentsCount);

        Assert.IsEmpty(
            viewModel.Items);

        Assert.IsFalse(
            viewModel.HasItems);

        Assert.IsNotNull(
            viewModel.Message);

        Assert.AreEqual(
            UserMessageSeverity.Success,
            viewModel.Message.Severity);
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
                        Recents =
                        [
                            CreateEntry("1")
                        ]
                    }
            };

        RecentsViewModel viewModel =
            CreateViewModel(
                library);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.ReducedMotion =
            true;

        Assert.IsTrue(
            viewModel.Items[0]
                .ReducedMotion);
    }

    private static RecentsViewModel CreateViewModel(
        FakeLibraryCoordinator library)
    {
        return new RecentsViewModel(
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
                DateTimeOffset.UtcNow,

            LastCopiedAtUtc =
                DateTimeOffset.UtcNow,

            CopyCount =
                1
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

        public int ClearRecentsCount
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
            cancellationToken
                .ThrowIfCancellationRequested();

            ClearRecentsCount++;

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
                        @"C:\CopyGIF\recent.gif",

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
