using CopyGIF.Application.Settings;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Settings;

namespace CopyGIF.Presentation.Tests.Settings;

[TestClass]
public sealed class GeneralSettingsViewModelTests
{
    [TestMethod]
    public async Task LoadCommand_LoadsGeneralSettings()
    {
        AppSettings settings =
            new()
            {
                Hotkey =
                    "Ctrl+Shift+G",

                Window =
                    new WindowSettings
                    {
                        PlacementMode =
                            WindowPlacementMode.Center,

                        RememberWindowSize =
                            false
                    },

                Behavior =
                    new BehaviorSettings
                    {
                        CloseWhenFocusLost =
                            false,

                        HideAfterCopy =
                            false
                    },

                Startup =
                    new StartupSettings
                    {
                        StartWithWindows =
                            false
                    }
            };

        FakeSettingsCoordinator coordinator =
            new(
                settings);

        GeneralSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsLoaded);

        Assert.AreEqual(
            "Ctrl+Shift+G",
            viewModel.Hotkey);

        Assert.IsFalse(
            viewModel.StartWithWindows);

        Assert.IsFalse(
            viewModel.CloseWhenFocusLost);

        Assert.IsFalse(
            viewModel.HideAfterCopy);

        Assert.AreEqual(
            WindowPlacementMode.Center,
            viewModel.PlacementMode);

        Assert.IsFalse(
            viewModel.RememberWindowSize);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);
    }

    [TestMethod]
    public void PlacementModes_ContainsEverySupportedMode()
    {
        GeneralSettingsViewModel viewModel =
            new(
                new FakeSettingsCoordinator(
                    new AppSettings()));

        CollectionAssert.AreEquivalent(
            new[]
            {
                WindowPlacementMode.Mouse,
                WindowPlacementMode.Remember,
                WindowPlacementMode.Center
            },
            viewModel.PlacementModes
                .ToArray());
    }

    [TestMethod]
    public async Task SaveCommand_SavesOwnedGeneralSettings()
    {
        FakeSettingsCoordinator coordinator =
            new(
                new AppSettings());

        GeneralSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.Hotkey =
            "Ctrl+Alt+G";

        viewModel.StartWithWindows =
            false;

        viewModel.CloseWhenFocusLost =
            false;

        viewModel.HideAfterCopy =
            false;

        viewModel.PlacementMode =
            WindowPlacementMode.Remember;

        viewModel.RememberWindowSize =
            false;

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.IsNotNull(
            coordinator.LastSaveRequest);

        AppSettings saved =
            coordinator.LastSaveRequest!;

        Assert.AreEqual(
            "Ctrl+Alt+G",
            saved.Hotkey);

        Assert.IsFalse(
            saved.Startup.StartWithWindows);

        Assert.IsFalse(
            saved.Behavior.CloseWhenFocusLost);

        Assert.IsFalse(
            saved.Behavior.HideAfterCopy);

        Assert.AreEqual(
            WindowPlacementMode.Remember,
            saved.Window.PlacementMode);

        Assert.IsFalse(
            saved.Window.RememberWindowSize);

        Assert.AreEqual(
            UserMessageSeverity.Success,
            viewModel.Message?.Severity);
    }

    [TestMethod]
    public async Task SaveCommand_PreservesUnrelatedSettings()
    {
        AppSettings original =
            new()
            {
                Search =
                    new SearchSettings
                    {
                        ResultsPerSearch =
                            40,

                        DebounceMilliseconds =
                            700,

                        SearchHistoryLimit =
                            200
                    },

                Library =
                    new LibrarySettings
                    {
                        RecentLimit =
                            75,

                        FavoriteLimit =
                            250,

                        CustomStorageRoot =
                            @"D:\CopyGIF Library"
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
                            true,

                        CheckFrequency =
                            UpdateCheckFrequency.Weekly,

                        Mode =
                            UpdateMode.NotifyOnly
                    },

                Providers =
                    new ProviderSettings
                    {
                        ActiveProviderId =
                            "klipy",

                        DisplayMode =
                            ProviderDisplayMode.Single
                    }
            };

        FakeSettingsCoordinator coordinator =
            new(
                original);

        GeneralSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.Hotkey =
            "Ctrl+Shift+G";

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.IsNotNull(
            coordinator.LastSaveRequest);

        AppSettings saved =
            coordinator.LastSaveRequest!;

        Assert.AreEqual(
            original.Search,
            saved.Search);

        Assert.AreEqual(
            original.Library,
            saved.Library);

        Assert.AreEqual(
            original.Appearance,
            saved.Appearance);

        Assert.AreEqual(
            original.Updates,
            saved.Updates);

        Assert.AreEqual(
            original.Providers,
            saved.Providers);
    }

    [TestMethod]
    public async Task SaveCommand_ReloadsLatestSettingsBeforeSaving()
    {
        FakeSettingsCoordinator coordinator =
            new(
                new AppSettings());

        GeneralSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        coordinator.CurrentSettings =
            coordinator.CurrentSettings with
            {
                Search =
                    new SearchSettings
                    {
                        ResultsPerSearch =
                            36
                    }
            };

        viewModel.Hotkey =
            "Ctrl+Alt+G";

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.IsNotNull(
            coordinator.LastSaveRequest);

        Assert.AreEqual(
            36,
            coordinator.LastSaveRequest!
                .Search
                .ResultsPerSearch);
    }

    [TestMethod]
    public async Task SaveCommand_HotkeyConflict_AppliesEffectiveRollbackSettings()
    {
        AppSettings original =
            new()
            {
                Hotkey =
                    "Alt+G"
            };

        FakeSettingsCoordinator coordinator =
            new(
                original)
            {
                SaveHandler =
                    requested =>
                        SettingsSaveResult
                            .HotkeyRejected(
                                original,
                                HotkeyRegistrationResult
                                    .Failed(
                                        HotkeyRegistrationFailure
                                            .Conflict,
                                        "The requested hotkey is already in use."))
            };

        GeneralSettingsViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.Hotkey =
            "Ctrl+Alt+G";

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            "Alt+G",
            viewModel.Hotkey);

        Assert.AreEqual(
            AsyncOperationStatus.Failed,
            viewModel.OperationState.Status);

        Assert.AreEqual(
            UserMessageSeverity.Warning,
            viewModel.Message?.Severity);
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

        public Func<
            AppSettings,
            SettingsSaveResult>? SaveHandler
        {
            get;
            init;
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

            SettingsSaveResult result =
                SaveHandler?.Invoke(
                    settings) ??
                SettingsSaveResult.Success(
                    settings);

            CurrentSettings =
                result.EffectiveSettings;

            return Task.FromResult(
                result);
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
