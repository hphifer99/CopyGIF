using CopyGIF.Application.Library;
using CopyGIF.Application.Media;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Search;

namespace CopyGIF.Presentation.Tests.Search;

[TestClass]
public sealed class GifCardViewModelTests
{
    [TestMethod]
    public async Task CopyCommand_DownloadFailureReportsStageWithoutLeakingSourceDetails()
    {
        var model = CreateViewModel(CreateGif(), copyCoordinator: new FakeGifCopyCoordinator
        {
            Exception = new MediaDownloadException(MediaDownloadFailure.Network, "https://private.example/secret")
        });
        await model.CopyCommand.ExecuteAsync(null);
        StringAssert.Contains(model.Message!.Text, "download failed (Network)");
        Assert.IsFalse(model.Message.Text.Contains("private.example", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ThumbnailLoad_RecycledCardReusesResolvedSourceUntilRetry()
    {
        var preview = new FakePreviewCoordinator();
        var model = CreateViewModel(CreateGif(), previewCoordinator: preview);
        await model.LoadThumbnailCommand.ExecuteAsync(null);
        await model.LoadThumbnailCommand.ExecuteAsync(null);
        Assert.AreEqual(1, preview.ThumbnailCalls);
        await model.LoadThumbnailCommand.ExecuteAsync(true);
        Assert.AreEqual(2, preview.ThumbnailCalls);
    }

    [TestMethod]
    public void DisplayQuality_BoundsDecodeSizeAndPreservesPortraitOrientation()
    {
        var model = CreateViewModel(CreateGif() with { Width = 100, Height = 300 });
        Assert.IsTrue(model.DecodeByHeight);
        model.DisplayQuality = CopyGIF.Core.Settings.GifQuality.Minimum;
        Assert.AreEqual(128, model.DecodePixelSize);
        model.DisplayQuality = CopyGIF.Core.Settings.GifQuality.Maximum;
        Assert.AreEqual(512, model.DecodePixelSize);
    }

    [TestMethod]
    public void Constructor_ExposesGifAndInitialState()
    {
        GifItem item =
            CreateGif();

        FakeGifCopyCoordinator copyCoordinator =
            new();

        FakeGifLibraryCoordinator libraryCoordinator =
            new();

        FakePreviewCoordinator previewCoordinator =
            new();

        GifCardViewModel viewModel =
            new(
                item,
                copyCoordinator,
                libraryCoordinator,
                previewCoordinator,
                isFavorite: true,
                searchQuery: "  cats  ",
                reducedMotion: false);

        Assert.AreSame(
            item,
            viewModel.Item);

        Assert.AreEqual(
            item.StableIdentity,
            viewModel.Identity);

        Assert.AreEqual(
            "test",
            viewModel.ProviderId);

        Assert.AreEqual(
            "1",
            viewModel.Id);

        Assert.AreEqual(
            "Test GIF",
            viewModel.Title);

        Assert.AreEqual(
            "Test description",
            viewModel.Description);

        Assert.IsNull(viewModel.ThumbnailSource);
        Assert.IsNull(viewModel.CurrentSource);

        Assert.IsTrue(
            viewModel.IsFavorite);

        Assert.AreEqual(
            "Remove from Favorites",
            viewModel.FavoriteActionText);

        Assert.AreEqual(
            "cats",
            viewModel.SearchQuery);

        Assert.AreEqual(
            AsyncOperationStatus.Idle,
            viewModel.OperationState.Status);

        Assert.IsFalse(
            viewModel.IsBusy);

        Assert.IsNull(
            viewModel.Message);
    }

    [TestMethod]
    public async Task LoadThumbnailAsync_UsesPreviewCoordinator()
    {
        GifItem item =
            CreateGif();

        Uri cachedThumbnail =
            new(
                "file:///C:/CopyGIF/cache/thumb.gif");

        FakePreviewCoordinator previewCoordinator =
            new()
            {
                ThumbnailResult =
                    cachedThumbnail
            };

        GifCardViewModel viewModel =
            CreateViewModel(
                item,
                previewCoordinator:
                    previewCoordinator);

        await viewModel
            .LoadThumbnailAsync();

        Assert.AreEqual(
            cachedThumbnail,
            viewModel.ThumbnailSource);

        Assert.AreEqual(
            cachedThumbnail,
            viewModel.CurrentSource);

        Assert.AreSame(
            item,
            previewCoordinator.LastThumbnailItem);
    }

    [TestMethod]
    public async Task CopyCommand_CopiesGifAndReportsSuccess()
    {
        GifItem item =
            CreateGif();

        FakeGifCopyCoordinator copyCoordinator =
            new();

        GifCardViewModel viewModel =
            CreateViewModel(
                item,
                copyCoordinator:
                    copyCoordinator,
                searchQuery:
                    "  funny cats  ");

        await viewModel
            .CopyCommand
            .ExecuteAsync(null);

        Assert.AreSame(
            item,
            copyCoordinator.LastItem);

        Assert.AreEqual(
            "funny cats",
            copyCoordinator.LastSearchQuery);

        Assert.AreEqual(
            1,
            copyCoordinator.CopyCount);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);

        Assert.IsFalse(
            viewModel.IsBusy);

        Assert.IsNotNull(
            viewModel.Message);

        Assert.AreEqual(
            UserMessageSeverity.Success,
            viewModel.Message.Severity);

        Assert.AreEqual(
            "GIF copied to the clipboard.",
            viewModel.Message.Text);
    }

    [TestMethod]
    public async Task CopyCommand_WhenCoordinatorFails_ReportsError()
    {
        FakeGifCopyCoordinator copyCoordinator =
            new()
            {
                Exception =
                    new InvalidOperationException(
                        "Test failure.")
            };

        GifCardViewModel viewModel =
            CreateViewModel(
                CreateGif(),
                copyCoordinator:
                    copyCoordinator);

        await viewModel
            .CopyCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            AsyncOperationStatus.Failed,
            viewModel.OperationState.Status);

        Assert.IsTrue(
            viewModel.OperationState.HasError);

        Assert.IsNotNull(
            viewModel.Message);

        Assert.AreEqual(
            UserMessageSeverity.Error,
            viewModel.Message.Severity);

        StringAssert.Contains(viewModel.Message.Text, "InvalidOperationException");
        Assert.AreEqual("gif_copy_failed", viewModel.Message.Code);
        Assert.IsFalse(viewModel.Message.Text.Contains("Test failure.", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ToggleFavoriteCommand_AddsFavorite()
    {
        GifItem item =
            CreateGif();

        FakeGifLibraryCoordinator libraryCoordinator =
            new();

        GifCardViewModel viewModel =
            CreateViewModel(
                item,
                libraryCoordinator:
                    libraryCoordinator);

        Assert.IsFalse(
            viewModel.IsFavorite);

        await viewModel
            .ToggleFavoriteCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsFavorite);

        Assert.AreEqual(
            "Remove from Favorites",
            viewModel.FavoriteActionText);

        Assert.AreSame(
            item,
            libraryCoordinator.LastAddedItem);

        Assert.AreEqual(
            1,
            libraryCoordinator.AddFavoriteCount);

        Assert.AreEqual(
            0,
            libraryCoordinator.RemoveFavoriteCount);

        Assert.IsNotNull(
            viewModel.Message);

        Assert.AreEqual(
            "GIF added to Favorites.",
            viewModel.Message.Text);
    }

    [TestMethod]
    public async Task ToggleFavoriteCommand_RemovesFavorite()
    {
        GifItem item =
            CreateGif();

        FakeGifLibraryCoordinator libraryCoordinator =
            new();

        GifCardViewModel viewModel =
            CreateViewModel(
                item,
                libraryCoordinator:
                    libraryCoordinator,
                isFavorite:
                    true);

        await viewModel
            .ToggleFavoriteCommand
            .ExecuteAsync(null);

        Assert.IsFalse(
            viewModel.IsFavorite);

        Assert.AreEqual(
            "Add to Favorites",
            viewModel.FavoriteActionText);

        Assert.AreEqual(
            item.StableIdentity,
            libraryCoordinator.LastRemovedIdentity);

        Assert.AreEqual(
            0,
            libraryCoordinator.AddFavoriteCount);

        Assert.AreEqual(
            1,
            libraryCoordinator.RemoveFavoriteCount);

        Assert.IsNotNull(
            viewModel.Message);

        Assert.AreEqual(
            "GIF removed from Favorites.",
            viewModel.Message.Text);
    }

    [TestMethod]
    public async Task ToggleFavoriteCommand_WhenLimitReached_KeepsState()
    {
        FakeGifLibraryCoordinator libraryCoordinator =
            new()
            {
                AddFavoriteException =
                    new InvalidOperationException(
                        "The favorite limit of 100 has been reached.")
            };

        GifCardViewModel viewModel =
            CreateViewModel(
                CreateGif(),
                libraryCoordinator:
                    libraryCoordinator);

        await viewModel
            .ToggleFavoriteCommand
            .ExecuteAsync(null);

        Assert.IsFalse(
            viewModel.IsFavorite);

        Assert.AreEqual(
            AsyncOperationStatus.Failed,
            viewModel.OperationState.Status);

        Assert.IsNotNull(
            viewModel.Message);

        Assert.AreEqual(
            UserMessageSeverity.Warning,
            viewModel.Message.Severity);

        Assert.AreEqual(
            "The favorite limit of 100 has been reached.",
            viewModel.Message.Text);
    }

    [TestMethod]
    public async Task PreviewCommands_SwitchBetweenAnimatedAndThumbnailSources()
    {
        GifItem item =
            CreateGif();

        Uri thumbnail =
            new(
                "file:///C:/CopyGIF/cache/thumb.gif");

        Uri animated =
            new(
                "file:///C:/CopyGIF/cache/animated.gif");

        FakePreviewCoordinator previewCoordinator =
            new()
            {
                ThumbnailResult =
                    thumbnail,

                AnimatedResult =
                    animated
            };

        GifCardViewModel viewModel =
            CreateViewModel(
                item,
                previewCoordinator:
                    previewCoordinator);

        await viewModel
            .LoadThumbnailAsync();

        await viewModel
            .StartPreviewCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsPreviewing);

        Assert.AreEqual(
            animated,
            viewModel.CurrentSource);

        Assert.IsTrue(
            viewModel.StopPreviewCommand
                .CanExecute(null));

        viewModel.StopPreviewCommand
            .Execute(null);

        Assert.IsFalse(
            viewModel.IsPreviewing);

        Assert.AreEqual(
            thumbnail,
            viewModel.CurrentSource);

        Assert.IsFalse(
            viewModel.StopPreviewCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task StartPreviewCommand_PassesReducedMotionPreference()
    {
        FakePreviewCoordinator previewCoordinator =
            new();

        GifCardViewModel viewModel =
            CreateViewModel(
                CreateGif(),
                previewCoordinator:
                    previewCoordinator,
                reducedMotion:
                    true);

        await viewModel
            .StartPreviewCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            previewCoordinator.LastReducedMotion);
    }

    [TestMethod]
    public void SetFavoriteState_UpdatesFavoritePresentation()
    {
        GifCardViewModel viewModel =
            CreateViewModel(
                CreateGif());

        Assert.IsFalse(
            viewModel.IsFavorite);

        viewModel.SetFavoriteState(
            true);

        Assert.IsTrue(
            viewModel.IsFavorite);

        Assert.AreEqual(
            "Remove from Favorites",
            viewModel.FavoriteActionText);

        viewModel.SetFavoriteState(
            false);

        Assert.IsFalse(
            viewModel.IsFavorite);

        Assert.AreEqual(
            "Add to Favorites",
            viewModel.FavoriteActionText);
    }

    [TestMethod]
    public void ClearMessage_RemovesCurrentMessage()
    {
        GifCardViewModel viewModel =
            CreateViewModel(
                CreateGif());

        viewModel.SetFavoriteState(
            true);

        viewModel.ClearMessage();

        Assert.IsNull(
            viewModel.Message);
    }

    private static GifCardViewModel CreateViewModel(
        GifItem item,
        FakeGifCopyCoordinator? copyCoordinator = null,
        FakeGifLibraryCoordinator? libraryCoordinator = null,
        FakePreviewCoordinator? previewCoordinator = null,
        bool isFavorite = false,
        string? searchQuery = null,
        bool reducedMotion = false)
    {
        return new GifCardViewModel(
            item,
            copyCoordinator ??
                new FakeGifCopyCoordinator(),
            libraryCoordinator ??
                new FakeGifLibraryCoordinator(),
            previewCoordinator ??
                new FakePreviewCoordinator(),
            isFavorite,
            searchQuery,
            reducedMotion);
    }

    private static GifItem CreateGif()
    {
        return new GifItem
        {
            ProviderId =
                "test",

            Id =
                "1",

            Title =
                "Test GIF",

            Description =
                "Test description",

            ThumbnailUri =
                new Uri(
                    "https://example.com/thumb.gif"),

            PreviewUri =
                new Uri(
                    "https://example.com/preview.gif"),

            GifUri =
                new Uri(
                    "https://example.com/full.gif"),

            SourcePageUri =
                new Uri(
                    "https://example.com/gif/1"),

            Width =
                640,

            Height =
                480,

            SizeBytes =
                1024
        };
    }

    private sealed class FakeGifCopyCoordinator :
        IGifCopyCoordinator
    {
        public GifItem? LastItem
        {
            get;
            private set;
        }

        public string? LastSearchQuery
        {
            get;
            private set;
        }

        public int CopyCount
        {
            get;
            private set;
        }

        public Exception? Exception
        {
            get;
            init;
        }

        public Task<DownloadedGif> CopyAsync(
            GifItem item,
            string? searchQuery,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastItem =
                item;

            LastSearchQuery =
                searchQuery;

            CopyCount++;

            if (Exception is not null)
            {
                return Task.FromException<DownloadedGif>(
                    Exception);
            }

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
                        item.SizeBytes ??
                        1024,

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

    private sealed class FakeGifLibraryCoordinator :
        IGifLibraryCoordinator
    {
        public GifItem? LastAddedItem
        {
            get;
            private set;
        }

        public GifIdentity? LastRemovedIdentity
        {
            get;
            private set;
        }

        public int AddFavoriteCount
        {
            get;
            private set;
        }

        public int RemoveFavoriteCount
        {
            get;
            private set;
        }

        public Exception? AddFavoriteException
        {
            get;
            init;
        }

        public Exception? RemoveFavoriteException
        {
            get;
            init;
        }

        public Task<LibrarySnapshot> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateEmptySnapshot());
        }

        public Task<LibrarySnapshot> AddFavoriteAsync(
            GifItem item,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastAddedItem =
                item;

            AddFavoriteCount++;

            if (AddFavoriteException is not null)
            {
                return Task.FromException<LibrarySnapshot>(
                    AddFavoriteException);
            }

            return Task.FromResult(
                CreateEmptySnapshot());
        }

        public Task<LibrarySnapshot> RemoveFavoriteAsync(
            GifIdentity identity,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastRemovedIdentity =
                identity;

            RemoveFavoriteCount++;

            if (RemoveFavoriteException is not null)
            {
                return Task.FromException<LibrarySnapshot>(
                    RemoveFavoriteException);
            }

            return Task.FromResult(
                CreateEmptySnapshot());
        }

        public Task<LibrarySnapshot> ClearFavoritesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateEmptySnapshot());
        }

        public Task<LibrarySnapshot> RecordRecentAsync(
            GifItem item,
            DownloadedGif copiedGif,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateEmptySnapshot());
        }

        public Task<LibrarySnapshot> ClearRecentsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                CreateEmptySnapshot());
        }

        private static LibrarySnapshot CreateEmptySnapshot()
        {
            return new LibrarySnapshot
            {
                Favorites = [],
                Recents = []
            };
        }
    }

    private sealed class FakePreviewCoordinator :
        IPreviewCoordinator
    {
        public int ThumbnailCalls { get; private set; }
        public GifItem? LastThumbnailItem
        {
            get;
            private set;
        }

        public GifItem? LastAnimatedItem
        {
            get;
            private set;
        }

        public bool LastReducedMotion
        {
            get;
            private set;
        }

        public Uri? ThumbnailResult
        {
            get;
            init;
        }

        public Uri? AnimatedResult
        {
            get;
            init;
        }

        public Task<Uri> GetThumbnailSourceAsync(
            GifItem item,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            ThumbnailCalls++;
            LastThumbnailItem =
                item;

            return Task.FromResult(
                ThumbnailResult ??
                item.ThumbnailUri);
        }

        public Task<Uri> GetAnimatedSourceAsync(
            GifItem item,
            bool reducedMotion,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastAnimatedItem =
                item;

            LastReducedMotion =
                reducedMotion;

            return Task.FromResult(
                AnimatedResult ??
                item.PreviewUri ??
                item.GifUri);
        }

        public Task InvalidateAsync(
            GifItem item,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }

        public Task CleanupAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }
}
