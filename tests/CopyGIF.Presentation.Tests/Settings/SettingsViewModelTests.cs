using CopyGIF.Application.Credentials;
using CopyGIF.Application.Onboarding;
using CopyGIF.Application.Settings;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Settings;

namespace CopyGIF.Presentation.Tests.Settings;

[TestClass]
public sealed class SettingsViewModelTests
{
    [TestMethod]
    public void Constructor_ExposesAllSections()
    {
        Harness harness =
            new();

        Assert.IsNotNull(
            harness.ViewModel.General);

        Assert.IsNotNull(
            harness.ViewModel.Search);

        Assert.IsNotNull(
            harness.ViewModel.Library);

        Assert.IsNotNull(
            harness.ViewModel.Appearance);

        Assert.IsNotNull(
            harness.ViewModel.Api);

        Assert.IsNotNull(
            harness.ViewModel.Updates);

        Assert.AreEqual(
            SettingsSection.General,
            harness.ViewModel.SelectedSection);
    }

    [TestMethod]
    public async Task LoadCommand_LoadsEverySection()
    {
        Harness harness =
            new();

        await harness.ViewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            harness.ViewModel.IsLoaded);

        Assert.IsTrue(
            harness.ViewModel.General.IsLoaded);

        Assert.IsTrue(
            harness.ViewModel.Search.IsLoaded);

        Assert.IsTrue(
            harness.ViewModel.Library.IsLoaded);

        Assert.IsTrue(
            harness.ViewModel.Appearance.IsLoaded);

        Assert.IsTrue(
            harness.ViewModel.Api.IsLoaded);

        Assert.IsTrue(
            harness.ViewModel.Updates.IsLoaded);

        Assert.IsFalse(
            harness.ViewModel.HasSectionErrors);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            harness.ViewModel.OperationState.Status);

        Assert.AreEqual(
            1,
            harness.Credentials.LoadCount);
    }

    [TestMethod]
    public async Task RestoreDefaultsCommand_RestoresSettingsWithoutDeletingCredential()
    {
        AppSettings customized =
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
                    }
            };

        Harness harness =
            new(
                customized);

        await harness.ViewModel
            .LoadCommand
            .ExecuteAsync(null);

        await harness.ViewModel
            .RestoreDefaultsCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            harness.Settings.RestoreCount);

        Assert.AreEqual(
            0,
            harness.Credentials.DeleteCount);

        Assert.AreEqual(
            AppSettings.DefaultHotkey,
            harness.ViewModel
                .General
                .Hotkey);

        Assert.AreEqual(
            24,
            harness.ViewModel
                .Search
                .ResultsPerSearch);

        Assert.AreEqual(
            AppTheme.System,
            harness.ViewModel
                .Appearance
                .Theme);

        Assert.IsTrue(
            harness.ViewModel.Api.HasCredential);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            harness.ViewModel.OperationState.Status);

        Assert.AreEqual(
            UserMessageSeverity.Success,
            harness.ViewModel.Message?.Severity);
    }

    [TestMethod]
    public async Task RestoreDefaultsCommand_HotkeyConflict_ShowsWarning()
    {
        AppSettings original =
            new()
            {
                Hotkey =
                    "Ctrl+Shift+G"
            };

        Harness harness =
            new(
                original);

        harness.Settings.RestoreHandler =
            () =>
                SettingsSaveResult.HotkeyRejected(
                    original,
                    HotkeyRegistrationResult.Failed(
                        HotkeyRegistrationFailure.Conflict,
                        "Alt+G is already registered."));

        await harness.ViewModel
            .LoadCommand
            .ExecuteAsync(null);

        await harness.ViewModel
            .RestoreDefaultsCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            AsyncOperationStatus.Failed,
            harness.ViewModel.OperationState.Status);

        Assert.AreEqual(
            UserMessageSeverity.Warning,
            harness.ViewModel.Message?.Severity);

        Assert.AreEqual(
            "default_hotkey_conflict",
            harness.ViewModel.Message?.Code);

        Assert.AreEqual(
            "Ctrl+Shift+G",
            harness.ViewModel
                .General
                .Hotkey);
    }

    [TestMethod]
    public void SelectedSection_CanBeChanged()
    {
        Harness harness =
            new();

        harness.ViewModel.SelectedSection =
            SettingsSection.Library;

        Assert.AreEqual(
            SettingsSection.Library,
            harness.ViewModel.SelectedSection);
    }

    private sealed class Harness
    {
        public Harness(
            AppSettings? settings = null)
        {
            Settings =
                new FakeSettingsCoordinator(
                    settings ??
                    new AppSettings());

            Credentials =
                new FakeCredentialCoordinator();

            FakeOnboardingCoordinator onboarding =
                new();

            GeneralSettingsViewModel general =
                new(
                    Settings);

            SearchSettingsViewModel search =
                new(
                    Settings);

            LibrarySettingsViewModel library =
                new(
                    Settings);

            AppearanceSettingsViewModel appearance =
                new(
                    Settings);

            ApiSettingsViewModel api =
                new(
                    Credentials,
                    onboarding);

            UpdateSettingsViewModel updates =
                new(
                    Settings);

            ViewModel =
                new SettingsViewModel(
                    Settings,
                    general,
                    search,
                    library,
                    appearance,
                    api,
                    updates);
        }

        public FakeSettingsCoordinator Settings
        { get; }

        public FakeCredentialCoordinator Credentials
        { get; }

        public SettingsViewModel ViewModel
        { get; }
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
            private set;
        }

        public int RestoreCount
        {
            get;
            private set;
        }

        public Func<SettingsSaveResult>?
            RestoreHandler
        {
            get;
            set;
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

            RestoreCount++;

            SettingsSaveResult result =
                RestoreHandler?.Invoke() ??
                SettingsSaveResult.Success(
                    new AppSettings());

            CurrentSettings =
                result.EffectiveSettings;

            return Task.FromResult(
                result);
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

    private sealed class FakeCredentialCoordinator :
        IApiCredentialCoordinator
    {
        public int LoadCount
        {
            get;
            private set;
        }

        public int DeleteCount
        {
            get;
            private set;
        }

        public Task<ApiCredentialState> GetStateAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LoadCount++;

            return Task.FromResult(
                new ApiCredentialState
                {
                    ProviderId =
                        "klipy",

                    ProviderDisplayName =
                        "KLIPY",

                    HasCredential =
                        true
                });
        }

        public Task<CredentialValidationResult>
            ValidateAndSaveAsync(
                string credential,
                CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                CredentialValidationResult.Valid());
        }

        public Task DeleteAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            DeleteCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeOnboardingCoordinator :
        IOnboardingCoordinator
    {
        public Uri CredentialHelpUri
        { get; } =
            new(
                "https://klipy.com/developers");

        public Task<OnboardingState> GetStateAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new OnboardingState
                {
                    IsRequired =
                        false,

                    ProviderId =
                        "klipy",

                    ProviderDisplayName =
                        "KLIPY",

                    CredentialHelpUri =
                        CredentialHelpUri
                });
        }

        public Task<CredentialValidationResult> CompleteAsync(
            string credential,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                CredentialValidationResult.Valid());
        }

        public Task<bool> OpenCredentialHelpAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                true);
        }
    }
}
