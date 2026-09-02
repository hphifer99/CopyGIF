using CopyGIF.Application.Providers;
using CopyGIF.Application.Setup;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Tests.Setup;

[TestClass]
public sealed class ProviderSetupCoordinatorTests
{
    [TestMethod]
    public async Task GetStateAsync_ReportsMissingCredential()
    {
        FakeCredentialManager manager =
            new();

        ProviderSetupCoordinator coordinator =
            CreateCoordinator(
                new FakeGifProvider(),
                manager);

        ProviderSetupState state =
            await coordinator.GetStateAsync();

        Assert.AreEqual(
            "test",
            state.ProviderId);

        Assert.AreEqual(
            "Test Provider",
            state.ProviderDisplayName);

        Assert.IsFalse(
            state.HasCredential);
    }

    [TestMethod]
    public async Task GetStateAsync_UsesConfiguredProviderSettings()
    {
        FakeProviderAccessor accessor =
            new(
                new FakeGifProvider());

        ProviderSetupCoordinator coordinator =
            new(
                accessor,
                new FakeSettingsStore(
                    CreateSettings(
                        "future")),
                [
                    new FakeCredentialManager()
                ]);

        await coordinator.GetStateAsync();

        Assert.AreEqual(
            "future",
            accessor.LastSettings!
                .Providers
                .ActiveProviderId);
    }

    [TestMethod]
    public async Task ValidateAndSaveCredentialAsync_SavesValidCredential()
    {
        FakeCredentialManager manager =
            new()
            {
                ValidationResult =
                    CredentialValidationResult.Valid()
            };

        ProviderSetupCoordinator coordinator =
            CreateCoordinator(
                new FakeGifProvider(),
                manager);

        CredentialValidationResult result =
            await coordinator
                .ValidateAndSaveCredentialAsync(
                    " valid-key ");

        Assert.IsTrue(
            result.IsValid);

        Assert.AreEqual(
            "valid-key",
            manager.SavedCredential);
    }

    [TestMethod]
    public async Task ValidateAndSaveCredentialAsync_DoesNotSaveInvalidCredential()
    {
        FakeCredentialManager manager =
            new()
            {
                ValidationResult =
                    CredentialValidationResult.Invalid(
                        "Invalid key.")
            };

        ProviderSetupCoordinator coordinator =
            CreateCoordinator(
                new FakeGifProvider(),
                manager);

        CredentialValidationResult result =
            await coordinator
                .ValidateAndSaveCredentialAsync(
                    "bad-key");

        Assert.IsFalse(
            result.IsValid);

        Assert.IsNull(
            manager.SavedCredential);
    }

    [TestMethod]
    public async Task ValidateAndSaveCredentialAsync_BlankCredential_DoesNotLoadSettings()
    {
        FakeSettingsStore settingsStore =
            new(
                new AppSettings());

        ProviderSetupCoordinator coordinator =
            new(
                new FakeProviderAccessor(
                    new FakeGifProvider()),
                settingsStore,
                [
                    new FakeCredentialManager()
                ]);

        CredentialValidationResult result =
            await coordinator
                .ValidateAndSaveCredentialAsync(
                    "   ");

        Assert.IsFalse(
            result.IsValid);

        Assert.AreEqual(
            CredentialValidationFailure.MissingCredential,
            result.Failure);

        Assert.AreEqual(
            0,
            settingsStore.LoadCount);
    }

    [TestMethod]
    public async Task ClearCredentialAsync_DeletesCredential()
    {
        FakeCredentialManager manager =
            new()
            {
                HasCredential = true
            };

        ProviderSetupCoordinator coordinator =
            CreateCoordinator(
                new FakeGifProvider(),
                manager);

        await coordinator.ClearCredentialAsync();

        Assert.IsTrue(
            manager.DeleteCalled);
    }

    private static ProviderSetupCoordinator
        CreateCoordinator(
            IGifProvider provider,
            IGifProviderCredentialManager manager)
    {
        return new ProviderSetupCoordinator(
            new FakeProviderAccessor(
                provider),
            new FakeSettingsStore(
                new AppSettings()),
            [
                manager
            ]);
    }

    private static AppSettings CreateSettings(
        string activeProviderId)
    {
        return new AppSettings
        {
            Providers =
                new ProviderSettings
                {
                    ActiveProviderId =
                        activeProviderId
                }
        };
    }

    private sealed class FakeProviderAccessor :
        IActiveGifProviderAccessor
    {
        private readonly IGifProvider
            _provider;

        public FakeProviderAccessor(
            IGifProvider provider)
        {
            _provider = provider;
        }

        public AppSettings? LastSettings
        {
            get;
            private set;
        }

        public IGifProvider GetActiveProvider(
            AppSettings settings)
        {
            LastSettings = settings;

            return _provider;
        }
    }

    private sealed class FakeSettingsStore :
        ISettingsStore
    {
        private AppSettings _settings;

        public FakeSettingsStore(
            AppSettings settings)
        {
            _settings = settings;
        }

        public int LoadCount
        {
            get;
            private set;
        }

        public Task<AppSettings> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LoadCount++;

            return Task.FromResult(
                _settings);
        }

        public Task SaveAsync(
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            _settings = settings;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeGifProvider :
        IGifProvider
    {
        public string Id => "test";

        public string DisplayName =>
            "Test Provider";

        public Task<GifSearchPage> SearchAsync(
            GifSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                GifSearchPage.Empty());
        }

        public Task<CredentialValidationResult>
            ValidateCredentialAsync(
                string credential,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                CredentialValidationResult.Valid());
        }

        public Task RegisterShareAsync(
            string itemId,
            string? searchQuery,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCredentialManager :
        IGifProviderCredentialManager
    {
        public string ProviderId => "test";

        public string DisplayName =>
            "Test Provider";

        public bool HasCredential
        {
            get;
            init;
        }

        public string? SavedCredential
        {
            get;
            private set;
        }

        public bool DeleteCalled
        {
            get;
            private set;
        }

        public CredentialValidationResult
            ValidationResult
        {
            get;
            init;
        } = CredentialValidationResult.Valid();

        public Task<bool> HasCredentialAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                HasCredential);
        }

        public Task<CredentialValidationResult>
            ValidateCredentialAsync(
                string credential,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                ValidationResult);
        }

        public Task SaveCredentialAsync(
            string credential,
            CancellationToken cancellationToken = default)
        {
            SavedCredential = credential;

            return Task.CompletedTask;
        }

        public Task DeleteCredentialAsync(
            CancellationToken cancellationToken = default)
        {
            DeleteCalled = true;

            return Task.CompletedTask;
        }
    }
}
