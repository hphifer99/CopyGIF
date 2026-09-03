using CopyGIF.Application.Credentials;
using CopyGIF.Application.Onboarding;
using CopyGIF.Application.Providers;
using CopyGIF.Core.Models;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Onboarding;

[TestClass]
public sealed class OnboardingCoordinatorTests
{
    [TestMethod]
    public async Task GetStateAsync_MissingCredential_RequiresOnboarding()
    {
        FakeGifProviderCredentialManager manager =
            new();

        OnboardingCoordinator coordinator =
            CreateCoordinator(
                manager);

        OnboardingState state =
            await coordinator.GetStateAsync();

        Assert.IsTrue(
            state.IsRequired);

        Assert.AreEqual(
            "klipy",
            state.ProviderId);

        Assert.AreEqual(
            "KLIPY",
            state.ProviderDisplayName);

        Assert.AreEqual(
            new Uri(
                "https://klipy.com/developers"),
            state.CredentialHelpUri);
    }

    [TestMethod]
    public async Task GetStateAsync_ExistingCredential_SkipsOnboarding()
    {
        FakeGifProviderCredentialManager manager =
            new()
            {
                StoredCredential =
                    "working-key"
            };

        OnboardingCoordinator coordinator =
            CreateCoordinator(
                manager);

        OnboardingState state =
            await coordinator.GetStateAsync();

        Assert.IsFalse(
            state.IsRequired);
    }

    [TestMethod]
    public async Task CompleteAsync_ValidCredential_CompletesOnboarding()
    {
        FakeGifProviderCredentialManager manager =
            new();

        OnboardingCoordinator coordinator =
            CreateCoordinator(
                manager);

        CredentialValidationResult result =
            await coordinator.CompleteAsync(
                "valid-key");

        OnboardingState state =
            await coordinator.GetStateAsync();

        Assert.IsTrue(
            result.IsValid);

        Assert.AreEqual(
            "valid-key",
            manager.StoredCredential);

        Assert.IsFalse(
            state.IsRequired);
    }

    [TestMethod]
    public async Task CompleteAsync_InvalidCredential_RemainsRequired()
    {
        FakeGifProviderCredentialManager manager =
            new()
            {
                ValidationHandler =
                    static (_, _) =>
                        Task.FromResult(
                            CredentialValidationResult.Invalid(
                                "The API key was not accepted."))
            };

        OnboardingCoordinator coordinator =
            CreateCoordinator(
                manager);

        CredentialValidationResult result =
            await coordinator.CompleteAsync(
                "invalid-key");

        OnboardingState state =
            await coordinator.GetStateAsync();

        Assert.IsFalse(
            result.IsValid);

        Assert.IsTrue(
            state.IsRequired);
    }

    [TestMethod]
    public async Task OpenCredentialHelpAsync_UsesOfficialDeveloperUri()
    {
        FakeUriLauncherService uriLauncher =
            new();

        OnboardingCoordinator coordinator =
            CreateCoordinator(
                new FakeGifProviderCredentialManager(),
                uriLauncher);

        bool opened =
            await coordinator.OpenCredentialHelpAsync();

        Assert.IsTrue(
            opened);

        Assert.AreEqual(
            1,
            uriLauncher.LaunchRequests.Count);

        Assert.AreEqual(
            new Uri(
                "https://klipy.com/developers"),
            uriLauncher.LaunchRequests[0]);
    }

    private static OnboardingCoordinator
        CreateCoordinator(
            FakeGifProviderCredentialManager manager,
            FakeUriLauncherService? uriLauncher = null)
    {
        FakeGifProvider provider =
            new();

        ProviderCatalog catalog =
            new(
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

        ApiCredentialCoordinator credentialCoordinator =
            new(
                new ActiveGifProviderAccessor(
                    catalog),
                new FakeSettingsStore(),
                [
                    manager
                ]);

        return new OnboardingCoordinator(
            credentialCoordinator,
            uriLauncher ??
                new FakeUriLauncherService());
    }
}
