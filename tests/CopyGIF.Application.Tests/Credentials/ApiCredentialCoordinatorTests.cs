using CopyGIF.Application.Credentials;
using CopyGIF.Application.Providers;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Credentials;

[TestClass]
public sealed class ApiCredentialCoordinatorTests
{
    [TestMethod]
    public async Task GetStateAsync_MissingCredential_ReportsConfiguredProvider()
    {
        FakeGifProviderCredentialManager manager =
            new();

        ApiCredentialCoordinator coordinator =
            CreateCoordinator(
                manager);

        ApiCredentialState state =
            await coordinator.GetStateAsync();

        Assert.AreEqual(
            "klipy",
            state.ProviderId);

        Assert.AreEqual(
            "KLIPY",
            state.ProviderDisplayName);

        Assert.IsFalse(
            state.HasCredential);
    }

    [TestMethod]
    public async Task GetStateAsync_ExistingCredential_IsReported()
    {
        FakeGifProviderCredentialManager manager =
            new()
            {
                StoredCredential =
                    "existing-key"
            };

        ApiCredentialCoordinator coordinator =
            CreateCoordinator(
                manager);

        ApiCredentialState state =
            await coordinator.GetStateAsync();

        Assert.IsTrue(
            state.HasCredential);
    }

    [TestMethod]
    public async Task ValidateAndSaveAsync_ValidCredential_TrimsAndSavesIt()
    {
        FakeGifProviderCredentialManager manager =
            new();

        ApiCredentialCoordinator coordinator =
            CreateCoordinator(
                manager);

        CredentialValidationResult result =
            await coordinator.ValidateAndSaveAsync(
                "  valid-key  ");

        Assert.IsTrue(
            result.IsValid);

        Assert.AreEqual(
            "valid-key",
            manager.StoredCredential);

        CollectionAssert.AreEqual(
            new[]
            {
                "valid-key"
            },
            manager.ValidationAttempts.ToArray());

        CollectionAssert.AreEqual(
            new[]
            {
                "valid-key"
            },
            manager.SaveAttempts.ToArray());
    }

    [TestMethod]
    public async Task ValidateAndSaveAsync_InvalidReplacement_PreservesPreviousCredential()
    {
        FakeGifProviderCredentialManager manager =
            new()
            {
                StoredCredential =
                    "working-key",

                ValidationHandler =
                    static (_, _) =>
                        Task.FromResult(
                            CredentialValidationResult.Invalid(
                                "The API key was not accepted."))
            };

        ApiCredentialCoordinator coordinator =
            CreateCoordinator(
                manager);

        CredentialValidationResult result =
            await coordinator.ValidateAndSaveAsync(
                "invalid-key");

        Assert.IsFalse(
            result.IsValid);

        Assert.AreEqual(
            "working-key",
            manager.StoredCredential);

        Assert.AreEqual(
            0,
            manager.SaveAttempts.Count);
    }

    [TestMethod]
    public async Task ValidateAndSaveAsync_SaveFailure_PreservesPreviousCredential()
    {
        FakeGifProviderCredentialManager manager =
            new()
            {
                StoredCredential =
                    "working-key",

                SaveHandler =
                    static (_, _) =>
                        throw new IOException(
                            "Secret storage failed.")
            };

        ApiCredentialCoordinator coordinator =
            CreateCoordinator(
                manager);

        await Assert.ThrowsAsync<IOException>(
            () => coordinator.ValidateAndSaveAsync(
                "replacement-key"));

        Assert.AreEqual(
            "working-key",
            manager.StoredCredential);
    }

    [TestMethod]
    public async Task ValidateAndSaveAsync_BlankCredential_DoesNotLoadSettings()
    {
        FakeSettingsStore settingsStore =
            new();

        ApiCredentialCoordinator coordinator =
            CreateCoordinator(
                new FakeGifProviderCredentialManager(),
                settingsStore);

        CredentialValidationResult result =
            await coordinator.ValidateAndSaveAsync(
                "   ");

        Assert.IsFalse(
            result.IsValid);

        Assert.AreEqual(
            CredentialValidationFailure.MissingCredential,
            result.Failure);

        Assert.AreEqual(
            0,
            settingsStore.LoadCallCount);
    }

    [TestMethod]
    public async Task DeleteAsync_DeletesActiveProviderCredential()
    {
        FakeGifProviderCredentialManager manager =
            new()
            {
                StoredCredential =
                    "working-key"
            };

        ApiCredentialCoordinator coordinator =
            CreateCoordinator(
                manager);

        await coordinator.DeleteAsync();

        Assert.IsNull(
            manager.StoredCredential);

        Assert.AreEqual(
            1,
            manager.DeleteCallCount);
    }

    [TestMethod]
    public void Constructor_DuplicateCredentialManagers_AreRejected()
    {
        FakeGifProvider provider =
            new();

        ProviderCatalog catalog =
            CreateCatalog(
                provider);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => new ApiCredentialCoordinator(
                new ActiveGifProviderAccessor(
                    catalog),
                new FakeSettingsStore(),
                [
                    new FakeGifProviderCredentialManager(),
                    new FakeGifProviderCredentialManager()
                ]));
    }

    private static ApiCredentialCoordinator
        CreateCoordinator(
            FakeGifProviderCredentialManager manager,
            FakeSettingsStore? settingsStore = null)
    {
        FakeGifProvider provider =
            new();

        return new ApiCredentialCoordinator(
            new ActiveGifProviderAccessor(
                CreateCatalog(
                    provider)),
            settingsStore ??
                new FakeSettingsStore
                {
                    Value = new AppSettings()
                },
            [
                manager
            ]);
    }

    private static ProviderCatalog CreateCatalog(
        FakeGifProvider provider)
    {
        return new ProviderCatalog(
            [
                provider
            ],
            [
                new ProviderDescriptor
                {
                    Id = provider.Id,
                    DisplayName = provider.DisplayName,

                    Capabilities =
                        ProviderCapabilities.Search |
                        ProviderCapabilities.CredentialValidation,

                    RequiresCredential = true
                }
            ]);
    }
}
