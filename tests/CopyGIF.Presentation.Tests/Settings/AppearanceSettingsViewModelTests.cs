using CopyGIF.Application.Settings;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Settings;

namespace CopyGIF.Presentation.Tests.Settings;

[TestClass]
public sealed class AppearanceSettingsViewModelTests
{
    [TestMethod]
    public async Task LoadCommand_LoadsTheme()
    {
        AppSettings settings =
            new()
            {
                Appearance =
                    new AppearanceSettings
                    {
                        Theme =
                            AppTheme.Dark
                    }
            };

        FakeSettingsCoordinator coordinator =
            new(
                settings);

        AppearanceSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsLoaded);

        Assert.AreEqual(
            AppTheme.Dark,
            viewModel.Theme);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);
    }

    [TestMethod]
    public void Themes_ContainsEverySupportedTheme()
    {
        AppearanceSettingsViewModel viewModel =
            new(
                new FakeSettingsCoordinator(
                    new AppSettings()));

        CollectionAssert.AreEquivalent(
            new[]
            {
                AppTheme.System,
                AppTheme.Light,
                AppTheme.Dark
            },
            viewModel.Themes
                .ToArray());
    }

    [TestMethod]
    public async Task SaveCommand_SavesSelectedTheme()
    {
        FakeSettingsCoordinator coordinator =
            new(
                new AppSettings());

        AppearanceSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.Theme =
            AppTheme.Dark;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.IsNotNull(
            coordinator.LastSaveRequest);

        Assert.AreEqual(
            AppTheme.Dark,
            coordinator.LastSaveRequest
                .Appearance
                .Theme);

        Assert.AreEqual(
            UserMessageSeverity.Success,
            viewModel.Message?.Severity);
    }

    [TestMethod]
    public async Task SaveCommand_PreservesOtherSettings()
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
                            42
                    },

                Library =
                    new LibrarySettings
                    {
                        FavoriteLimit =
                            200
                    }
            };

        FakeSettingsCoordinator coordinator =
            new(
                original);

        AppearanceSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.Theme =
            AppTheme.Light;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            original.Hotkey,
            coordinator.LastSaveRequest?.Hotkey);

        Assert.AreEqual(
            original.Search,
            coordinator.LastSaveRequest?.Search);

        Assert.AreEqual(
            original.Library,
            coordinator.LastSaveRequest?.Library);
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
