using CopyGIF.Application.Settings;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Settings;

namespace CopyGIF.Presentation.Tests.Settings;

[TestClass]
public sealed class UpdateSettingsViewModelTests
{
    [TestMethod]
    public async Task LoadCommand_LoadsUpdateSettings()
    {
        AppSettings settings =
            new()
            {
                Updates =
                    new Core.Settings.UpdateSettings
                    {
                        CheckForUpdates =
                            false,

                        CheckFrequency =
                            UpdateCheckFrequency.Weekly,

                        Mode =
                            UpdateMode.NotifyOnly
                    }
            };

        UpdateSettingsViewModel viewModel =
            new(
                new FakeSettingsCoordinator(
                    settings));

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsLoaded);

        Assert.IsFalse(
            viewModel.CheckForUpdates);

        Assert.AreEqual(
            UpdateCheckFrequency.Weekly,
            viewModel.CheckFrequency);

        Assert.AreEqual(
            UpdateMode.NotifyOnly,
            viewModel.Mode);

        Assert.IsTrue(
            viewModel.IsValid);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);
    }

    [TestMethod]
    public void Collections_ContainAllSupportedValues()
    {
        UpdateSettingsViewModel viewModel =
            new(
                new FakeSettingsCoordinator(
                    new AppSettings()));

        CollectionAssert.AreEquivalent(
            new[]
            {
                UpdateCheckFrequency.Daily,
                UpdateCheckFrequency.Weekly
            },
            viewModel.CheckFrequencies
                .ToArray());

        CollectionAssert.AreEquivalent(
            new[]
            {
                UpdateMode.Recommended,
                UpdateMode.NotifyOnly,
                UpdateMode.DownloadAndPrompt,
                UpdateMode.DownloadAndInstall
            },
            viewModel.Modes
                .ToArray());
    }

    [TestMethod]
    public async Task SaveCommand_SavesUpdateSettings()
    {
        FakeSettingsCoordinator coordinator =
            new(
                new AppSettings());

        UpdateSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.CheckForUpdates =
            false;

        viewModel.CheckFrequency =
            UpdateCheckFrequency.Weekly;

        viewModel.Mode =
            UpdateMode.NotifyOnly;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.IsNotNull(
            coordinator.LastSaveRequest);

        Core.Settings.UpdateSettings saved =
            coordinator.LastSaveRequest
                .Updates;

        Assert.IsFalse(
            saved.CheckForUpdates);

        Assert.AreEqual(
            UpdateCheckFrequency.Weekly,
            saved.CheckFrequency);

        Assert.AreEqual(
            UpdateMode.NotifyOnly,
            saved.Mode);

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
                            40
                    },

                Library =
                    new LibrarySettings
                    {
                        FavoriteLimit =
                            250
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

        UpdateSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.Mode =
            UpdateMode.DownloadAndPrompt;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.IsNotNull(
            coordinator.LastSaveRequest);

        Assert.AreEqual(
            original.Hotkey,
            coordinator.LastSaveRequest.Hotkey);

        Assert.AreEqual(
            original.Search,
            coordinator.LastSaveRequest.Search);

        Assert.AreEqual(
            original.Library,
            coordinator.LastSaveRequest.Library);

        Assert.AreEqual(
            original.Appearance,
            coordinator.LastSaveRequest.Appearance);
    }

    [TestMethod]
    public async Task SaveCommand_UsesLatestSettings()
    {
        FakeSettingsCoordinator coordinator =
            new(
                new AppSettings());

        UpdateSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        coordinator.CurrentSettings =
            coordinator.CurrentSettings with
            {
                Hotkey =
                    "Ctrl+Alt+G"
            };

        viewModel.CheckFrequency =
            UpdateCheckFrequency.Weekly;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.IsNotNull(
            coordinator.LastSaveRequest);

        Assert.AreEqual(
            "Ctrl+Alt+G",
            coordinator.LastSaveRequest.Hotkey);
    }

    [TestMethod]
    public async Task SaveCommand_InvalidMode_IsDisabled()
    {
        UpdateSettingsViewModel viewModel =
            new(
                new FakeSettingsCoordinator(
                    new AppSettings()));

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.Mode =
            (UpdateMode)999;

        Assert.IsFalse(
            viewModel.IsValid);

        Assert.IsFalse(
            viewModel.SaveCommand
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

            return Task.FromResult<
                SettingsSaveResult?>(
                    null);
        }
    }
}
