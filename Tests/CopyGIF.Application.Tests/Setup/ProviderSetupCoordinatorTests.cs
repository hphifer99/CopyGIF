using CopyGIF.Application.Providers;
using CopyGIF.Application.Setup;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Application.Tests.Setup;

[TestClass]
public sealed class ProviderSetupCoordinatorTests
{
    [TestMethod]
    public async Task GetStateAsync_ReportsMissingCredential()
    {
        FakeGifProvider provider =
            new();

        FakeCredentialManager manager =
            new();

        ProviderSetupCoordinator coordinator =
            CreateCoordinator(
                provider,
                manager);

        ProviderSetupState state =
            await coordinator
                .GetStateAsync();

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
    public async Task ValidateAndSaveCredentialAsync_SavesValidCredential()
    {
        FakeGifProvider provider =
            new();

        FakeCredentialManager manager =
            new()
            {
                ValidationResult =
                    CredentialValidationResult
                        .Valid()
            };

        ProviderSetupCoordinator coordinator =
            CreateCoordinator(
                provider,
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
        FakeGifProvider provider =
            new();

        FakeCredentialManager manager =
            new()
            {
                ValidationResult =
                    CredentialValidationResult
                        .Invalid(
                            "Invalid key.")
            };

        ProviderSetupCoordinator coordinator =
            CreateCoordinator(
                provider,
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
    public async Task ClearCredentialAsync_DeletesCredential()
    {
        FakeGifProvider provider =
            new();

        FakeCredentialManager manager =
            new()
            {
                HasCredential =
                    true
            };

        ProviderSetupCoordinator coordinator =
            CreateCoordinator(
                provider,
                manager);

        await coordinator
            .ClearCredentialAsync();

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
            new[]
            {
                manager
            });
    }

    private sealed class FakeProviderAccessor :
        IActiveGifProviderAccessor
    {
        private readonly IGifProvider
            _provider;

        public FakeProviderAccessor(
            IGifProvider provider)
        {
            _provider =
                provider;
        }

        public IGifProvider
            GetActiveProvider()
        {
            return _provider;
        }
    }

    private sealed class FakeGifProvider :
        IGifProvider
    {
        public string Id =>
            "test";

        public string DisplayName =>
            "Test Provider";

        public Task<GifSearchPage>
            SearchAsync(
                GifSearchRequest request,
                CancellationToken cancellationToken =
                    default)
        {
            return Task.FromResult(
                new GifSearchPage
                {
                    Items = []
                });
        }

        public Task<CredentialValidationResult>
            ValidateCredentialAsync(
                string credential,
                CancellationToken cancellationToken =
                    default)
        {
            return Task.FromResult(
                CredentialValidationResult.Valid());
        }

        public Task RegisterShareAsync(
            string itemId,
            string? searchQuery,
            CancellationToken cancellationToken =
                default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCredentialManager :
        IGifProviderCredentialManager
    {
        public string ProviderId =>
            "test";

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
        } =
            CredentialValidationResult.Valid();

        public Task<bool>
            HasCredentialAsync(
                CancellationToken cancellationToken =
                    default)
        {
            return Task.FromResult(
                HasCredential);
        }

        public Task<CredentialValidationResult>
            ValidateCredentialAsync(
                string credential,
                CancellationToken cancellationToken =
                    default)
        {
            return Task.FromResult(
                ValidationResult);
        }

        public Task SaveCredentialAsync(
            string credential,
            CancellationToken cancellationToken =
                default)
        {
            SavedCredential =
                credential;

            return Task.CompletedTask;
        }

        public Task DeleteCredentialAsync(
            CancellationToken cancellationToken =
                default)
        {
            DeleteCalled =
                true;

            return Task.CompletedTask;
        }
    }
}