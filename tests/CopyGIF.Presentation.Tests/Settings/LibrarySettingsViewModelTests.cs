using CopyGIF.Application.Settings;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Settings;

namespace CopyGIF.Presentation.Tests.Settings;

[TestClass]
public sealed class LibrarySettingsViewModelTests
{
    [TestMethod]
    public async Task LoadCommand_LoadsLibrarySettings()
    {
        AppSettings settings =
            new()
            {
                Library =
                    new LibrarySettings
                    {
                        RecentLimit =
                            75,

                        FavoriteLimit =
                            250,

                        StoreFavoritesLocally =
                            false,

                        StoreRecentsLocally =
                            false,

                        CustomStorageRoot =
                            @"D:\CopyGIF Library"
                    }
            };

        FakeSettingsCoordinator coordinator =
            new(
                settings);

        LibrarySettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsLoaded);

        Assert.AreEqual(
            75,
            viewModel.RecentLimit);

        Assert.AreEqual(
            250,
            viewModel.FavoriteLimit);

        Assert.IsFalse(
            viewModel.StoreFavoritesLocally);

        Assert.IsFalse(
            viewModel.StoreRecentsLocally);

        Assert.AreEqual(
            @"D:\CopyGIF Library",
            viewModel.CustomStorageRoot);

        Assert.IsTrue(
            viewModel.HasCustomStorageRoot);

        Assert.AreEqual(
            @"D:\CopyGIF Library",
            viewModel.StorageLocationText);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);
    }

    [TestMethod]
    public void ValidationRanges_MatchCoreValidator()
    {
        LibrarySettingsViewModel viewModel =
            new(
                new FakeSettingsCoordinator(
                    new AppSettings()));

        Assert.AreEqual(
            AppSettingsValidator.MinimumRecentLimit,
            viewModel.MinimumRecentLimit);

        Assert.AreEqual(
            AppSettingsValidator.MaximumRecentLimit,
            viewModel.MaximumRecentLimit);

        Assert.AreEqual(
            AppSettingsValidator.MinimumFavoriteLimit,
            viewModel.MinimumFavoriteLimit);

        Assert.AreEqual(
            AppSettingsValidator.MaximumFavoriteLimit,
            viewModel.MaximumFavoriteLimit);
    }

    [TestMethod]
    public async Task SaveCommand_SavesOwnedLibrarySettings()
    {
        FakeSettingsCoordinator coordinator =
            new(
                new AppSettings());

        LibrarySettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.RecentLimit =
            80;

        viewModel.FavoriteLimit =
            300;

        viewModel.StoreFavoritesLocally =
            false;

        viewModel.StoreRecentsLocally =
            false;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.IsNotNull(
            coordinator.LastSaveRequest);

        LibrarySettings saved =
            coordinator.LastSaveRequest!
                .Library;

        Assert.AreEqual(
            80,
            saved.RecentLimit);

        Assert.AreEqual(
            300,
            saved.FavoriteLimit);

        Assert.IsFalse(
            saved.StoreFavoritesLocally);

        Assert.IsFalse(
            saved.StoreRecentsLocally);

        Assert.AreEqual(
            UserMessageSeverity.Success,
            viewModel.Message?.Severity);
    }

    [TestMethod]
    public async Task SaveCommand_PreservesExistingStorageRoot()
    {
        AppSettings settings =
            new()
            {
                Library =
                    new LibrarySettings
                    {
                        CustomStorageRoot =
                            @"D:\CopyGIF Library"
                    }
            };

        FakeSettingsCoordinator coordinator =
            new(
                settings);

        LibrarySettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.RecentLimit =
            50;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.IsNotNull(
            coordinator.LastSaveRequest);

        Assert.AreEqual(
            @"D:\CopyGIF Library",
            coordinator.LastSaveRequest!
                .Library
                .CustomStorageRoot);
    }

    [TestMethod]
    public async Task SaveCommand_PreservesLatestStorageRoot()
    {
        AppSettings settings =
            new()
            {
                Library =
                    new LibrarySettings
                    {
                        CustomStorageRoot =
                            @"D:\Old Library"
                    }
            };

        FakeSettingsCoordinator coordinator =
            new(
                settings);

        LibrarySettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        coordinator.CurrentSettings =
            coordinator.CurrentSettings with
            {
                Library =
                    coordinator.CurrentSettings.Library with
                    {
                        CustomStorageRoot =
                            @"E:\New Library"
                    }
            };

        viewModel.FavoriteLimit =
            200;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.IsNotNull(
            coordinator.LastSaveRequest);

        Assert.AreEqual(
            @"E:\New Library",
            coordinator.LastSaveRequest!
                .Library
                .CustomStorageRoot);
    }

    [TestMethod]
    public async Task SaveCommand_PreservesUnrelatedSettings()
    {
        AppSettings original =
            new()
            {
                Hotkey =
                    "Ctrl+Shift+G",

                Search =
                    new SearchSettings
                    {
                        ResultsPerSearch =
                            40
                    },

                Appearance =
                    new AppearanceSettings
                    {
                        Theme =
                            AppTheme.Dark
                    },

                Updates =
                    new UpdateSettings
                    {
                        CheckForUpdates =
                            false,

                        CheckFrequency =
                            UpdateCheckFrequency.Weekly,

                        Mode =
                            UpdateMode.NotifyOnly
                    }
            };

        FakeSettingsCoordinator coordinator =
            new(
                original);

        LibrarySettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.RecentLimit =
            60;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.IsNotNull(
            coordinator.LastSaveRequest);

        AppSettings saved =
            coordinator.LastSaveRequest!;

        Assert.AreEqual(
            original.Hotkey,
            saved.Hotkey);

        Assert.AreEqual(
            original.Search,
            saved.Search);

        Assert.AreEqual(
            original.Appearance,
            saved.Appearance);

        Assert.AreEqual(
            original.Updates,
            saved.Updates);
    }

    [TestMethod]
    public async Task InvalidRecentLimit_DisablesSaveCommand()
    {
        LibrarySettingsViewModel viewModel =
            new(
                new FakeSettingsCoordinator(
                    new AppSettings()));

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.RecentLimit =
            AppSettingsValidator
                .MinimumRecentLimit -
            1;

        Assert.IsFalse(
            viewModel.IsValid);

        Assert.IsFalse(
            viewModel.SaveCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task InvalidFavoriteLimit_DisablesSaveCommand()
    {
        LibrarySettingsViewModel viewModel =
            new(
                new FakeSettingsCoordinator(
                    new AppSettings()));

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.FavoriteLimit =
            AppSettingsValidator
                .MaximumFavoriteLimit +
            1;

        Assert.IsFalse(
            viewModel.IsValid);

        Assert.IsFalse(
            viewModel.SaveCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task ChooseStorageRootCommand_UpdatesLocation()
    {
        FakeSettingsCoordinator coordinator =
            new(
                new AppSettings());

        coordinator.ChooseResult =
            SettingsSaveResult.Success(
                coordinator.CurrentSettings with
                {
                    Library =
                        coordinator.CurrentSettings.Library with
                        {
                            CustomStorageRoot =
                                @"E:\GIF Library"
                        }
                });

        LibrarySettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        await viewModel
            .ChooseStorageRootCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            coordinator.ChooseCount);

        Assert.AreEqual(
            @"E:\GIF Library",
            viewModel.CustomStorageRoot);

        Assert.AreEqual(
            @"E:\GIF Library",
            viewModel.StorageLocationText);

        Assert.IsTrue(
            viewModel.HasCustomStorageRoot);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);

        Assert.AreEqual(
            UserMessageSeverity.Success,
            viewModel.Message?.Severity);
    }

    [TestMethod]
    public async Task ChooseStorageRootCommand_Cancelled_DoesNotChangeLocation()
    {
        FakeSettingsCoordinator coordinator =
            new(
                new AppSettings())
            {
                ChooseResult =
                    null
            };

        LibrarySettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        await viewModel
            .ChooseStorageRootCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            coordinator.ChooseCount);

        Assert.IsNull(
            viewModel.CustomStorageRoot);

        Assert.IsFalse(
            viewModel.HasCustomStorageRoot);

        Assert.AreEqual(
            "Default CopyGIF storage",
            viewModel.StorageLocationText);

        Assert.AreEqual(
            AsyncOperationStatus.Cancelled,
            viewModel.OperationState.Status);

        Assert.IsNull(
            viewModel.Message);
    }

    [TestMethod]
    public async Task ResetStorageRootCommand_RestoresDefaultLocation()
    {
        AppSettings settings =
            new()
            {
                Library =
                    new LibrarySettings
                    {
                        CustomStorageRoot =
                            @"D:\CopyGIF Library"
                    }
            };

        FakeSettingsCoordinator coordinator =
            new(
                settings);

        LibrarySettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.ResetStorageRootCommand
                .CanExecute(null));

        await viewModel
            .ResetStorageRootCommand
            .ExecuteAsync(null);

        Assert.IsNotNull(
            coordinator.LastSaveRequest);

        Assert.IsNull(
            coordinator.LastSaveRequest!
                .Library
                .CustomStorageRoot);

        Assert.IsNull(
            viewModel.CustomStorageRoot);

        Assert.IsFalse(
            viewModel.HasCustomStorageRoot);

        Assert.AreEqual(
            "Default CopyGIF storage",
            viewModel.StorageLocationText);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);
    }

    [TestMethod]
    public async Task ResetStorageRootCommand_IsDisabledForDefaultLocation()
    {
        LibrarySettingsViewModel viewModel =
            new(
                new FakeSettingsCoordinator(
                    new AppSettings()));

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsFalse(
            viewModel.HasCustomStorageRoot);

        Assert.IsFalse(
            viewModel.ResetStorageRootCommand
                .CanExecute(null));
    }

    private sealed class FakeSettingsCoordinator :
        ISettingsCoordinator
    {
        public FakeSettingsCoordinator(
            AppSettings settings)
        {
            CurrentSettings =
                settings;
        }

        public AppSettings CurrentSettings
        {
            get;
            set;
        }

        public AppSettings? LastSaveRequest
        {
            get;
            private set;
        }

        public SettingsSaveResult? ChooseResult
        {
            get;
            set;
        }

        public int ChooseCount
        {
            get;
            private set;
        }

        public Task<AppSettings> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                CurrentSettings);
        }

        public Task<SettingsSaveResult> SaveAsync(
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastSaveRequest =
                settings;

            CurrentSettings =
                settings;

            return Task.FromResult(
                SettingsSaveResult.Success(
                    settings));
        }

        public Task<SettingsSaveResult> RestoreDefaultsAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            CurrentSettings =
                new AppSettings();

            return Task.FromResult(
                SettingsSaveResult.Success(
                    CurrentSettings));
        }

        public Task<SettingsSaveResult?>
            ChooseLibraryStorageRootAsync(
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            ChooseCount++;

            if (ChooseResult is not null)
            {
                CurrentSettings =
                    ChooseResult
                        .EffectiveSettings;
            }

            return Task.FromResult(
                ChooseResult);
        }
    }
}
