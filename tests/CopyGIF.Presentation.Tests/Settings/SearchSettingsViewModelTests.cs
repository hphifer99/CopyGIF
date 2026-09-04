using CopyGIF.Application.Settings;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Settings;

namespace CopyGIF.Presentation.Tests.Settings;

[TestClass]
public sealed class SearchSettingsViewModelTests
{
    [TestMethod]
    public async Task LoadCommand_LoadsSearchSettings()
    {
        AppSettings settings =
            new()
            {
                Search =
                    new SearchSettings
                    {
                        ResultsPerSearch =
                            36,

                        DebounceMilliseconds =
                            500,

                        AnimatePreviews =
                            false,

                        AutoLoadMoreResults =
                            true,

                        ShowTrendingWhenEmpty =
                            false,

                        SaveSearchHistory =
                            false,

                        UseHistorySuggestions =
                            false,

                        SearchHistoryLimit =
                            75
                    }
            };

        SearchSettingsViewModel viewModel =
            new(
                new FakeSettingsCoordinator(
                    settings));

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            36,
            viewModel.ResultsPerSearch);

        Assert.AreEqual(
            500,
            viewModel.DebounceMilliseconds);

        Assert.IsFalse(
            viewModel.AnimatePreviews);

        Assert.IsTrue(
            viewModel.AutoLoadMoreResults);

        Assert.IsFalse(
            viewModel.ShowTrendingWhenEmpty);

        Assert.IsFalse(
            viewModel.SaveSearchHistory);

        Assert.IsFalse(
            viewModel.UseHistorySuggestions);

        Assert.AreEqual(
            75,
            viewModel.SearchHistoryLimit);

        Assert.IsTrue(
            viewModel.IsValid);
    }

    [TestMethod]
    public async Task SaveCommand_SavesSearchSettings()
    {
        FakeSettingsCoordinator coordinator =
            new(
                new AppSettings());

        SearchSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.ResultsPerSearch =
            40;

        viewModel.DebounceMilliseconds =
            450;

        viewModel.AnimatePreviews =
            false;

        viewModel.AutoLoadMoreResults =
            true;

        viewModel.ShowTrendingWhenEmpty =
            false;

        viewModel.SaveSearchHistory =
            false;

        viewModel.UseHistorySuggestions =
            false;

        viewModel.SearchHistoryLimit =
            100;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        SearchSettings saved =
            coordinator.LastSaveRequest!
                .Search;

        Assert.AreEqual(
            40,
            saved.ResultsPerSearch);

        Assert.AreEqual(
            450,
            saved.DebounceMilliseconds);

        Assert.IsFalse(
            saved.AnimatePreviews);

        Assert.IsTrue(
            saved.AutoLoadMoreResults);

        Assert.AreEqual(
            100,
            saved.SearchHistoryLimit);

        Assert.AreEqual(
            UserMessageSeverity.Success,
            viewModel.Message?.Severity);
    }

    [TestMethod]
    public async Task SaveCommand_InvalidResultsCount_IsDisabled()
    {
        SearchSettingsViewModel viewModel =
            new(
                new FakeSettingsCoordinator(
                    new AppSettings()));

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.ResultsPerSearch =
            AppSettingsValidator
                .MaximumResultsPerSearch +
            1;

        Assert.IsFalse(
            viewModel.IsValid);

        Assert.IsFalse(
            viewModel.SaveCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task SaveCommand_InvalidDebounce_IsDisabled()
    {
        SearchSettingsViewModel viewModel =
            new(
                new FakeSettingsCoordinator(
                    new AppSettings()));

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.DebounceMilliseconds =
            AppSettingsValidator
                .MinimumDebounceMilliseconds -
            1;

        Assert.IsFalse(
            viewModel.IsValid);

        Assert.IsFalse(
            viewModel.SaveCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task SaveCommand_PreservesOtherSettings()
    {
        AppSettings original =
            new()
            {
                Hotkey =
                    "Ctrl+Alt+G",

                Library =
                    new LibrarySettings
                    {
                        RecentLimit =
                            44
                    },

                Appearance =
                    new AppearanceSettings
                    {
                        Theme =
                            AppTheme.Dark
                    }
            };

        FakeSettingsCoordinator coordinator =
            new(
                original);

        SearchSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.ResultsPerSearch =
            48;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            original.Hotkey,
            coordinator.LastSaveRequest?.Hotkey);

        Assert.AreEqual(
            original.Library,
            coordinator.LastSaveRequest?.Library);

        Assert.AreEqual(
            original.Appearance,
            coordinator.LastSaveRequest?.Appearance);
    }

    [TestMethod]
    public async Task SaveCommand_UsesLatestSettings()
    {
        FakeSettingsCoordinator coordinator =
            new(
                new AppSettings());

        SearchSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        coordinator.CurrentSettings =
            coordinator.CurrentSettings with
            {
                Hotkey =
                    "Ctrl+Shift+G"
            };

        viewModel.ResultsPerSearch =
            30;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            "Ctrl+Shift+G",
            coordinator.LastSaveRequest?.Hotkey);
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
            return Task.FromResult<
                SettingsSaveResult?>(
                    null);
        }
    }
}
