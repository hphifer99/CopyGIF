using CopyGIF.Application.Credentials;
using CopyGIF.Application.Onboarding;
using CopyGIF.Core.Models;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Onboarding;

[TestClass]
public sealed class OnboardingProviderListTests
{
    private static readonly ProviderDescriptor Third = new()
    {
        Id = "third",
        DisplayName = "Third Gifs",
        CredentialHelpUri = new Uri("https://third.example/keys"),
        CredentialInstructions = "Pick the web key."
    };

    private static readonly ProviderDescriptor NoKeyNeeded = new()
    {
        Id = "open",
        DisplayName = "Open Gifs",
        RequiresCredential = false
    };

    [TestMethod]
    public void Providers_FollowTheRegisteredProviders_InOrder_AndSkipThoseWithoutKeys()
    {
        OnboardingCoordinator coordinator = Create(
            new FakeProviderCatalog(BuiltInProviders.Klipy, BuiltInProviders.Giphy, NoKeyNeeded, Third));

        CollectionAssert.AreEqual(
            new[] { "klipy", "giphy", "third" },
            coordinator.Providers.Select(option => option.Id).ToArray());
    }

    [TestMethod]
    public void Providers_CarryTheHelpLinkAndInstructionsOfEachDescriptor()
    {
        OnboardingCoordinator coordinator = Create(new FakeProviderCatalog());

        OnboardingProviderOption giphy = coordinator.Providers.Single(option => option.Id == "giphy");

        Assert.AreEqual("GIPHY", giphy.DisplayName);
        Assert.AreEqual(new Uri("https://developers.giphy.com/dashboard/"), giphy.CredentialHelpUri);
        Assert.AreEqual("For GIPHY, choose API instead of SDK.", giphy.CredentialInstructions);
        Assert.IsNull(coordinator.Providers.Single(option => option.Id == "klipy").CredentialInstructions);
    }

    [TestMethod]
    public async Task GetStateAsync_ForAThirdProvider_UsesItsOwnHelpLinkAndInstructions()
    {
        OnboardingCoordinator coordinator = Create(
            new FakeProviderCatalog(BuiltInProviders.Klipy, Third),
            new ApiCredentialState { ProviderId = "third", ProviderDisplayName = "Third Gifs", HasCredential = false });

        OnboardingState state = await coordinator.GetStateAsync();

        Assert.IsTrue(state.IsRequired);
        Assert.AreEqual(new Uri("https://third.example/keys"), state.CredentialHelpUri);
        Assert.AreEqual("Pick the web key.", state.CredentialInstructions);
    }

    [TestMethod]
    public async Task GetStateAsync_ForAProviderThatIsNoLongerRegistered_HasNoHelpLink()
    {
        OnboardingCoordinator coordinator = Create(
            new FakeProviderCatalog(BuiltInProviders.Klipy),
            new ApiCredentialState { ProviderId = "removed", ProviderDisplayName = "Removed", HasCredential = false });

        OnboardingState state = await coordinator.GetStateAsync();

        Assert.IsNull(state.CredentialHelpUri);
        Assert.IsNull(state.CredentialInstructions);
    }

    [TestMethod]
    public async Task OpenCredentialHelpAsync_OpensTheDefaultProvidersLink()
    {
        FakeUriLauncherService launcher = new();
        OnboardingCoordinator coordinator = Create(new FakeProviderCatalog(Third, BuiltInProviders.Klipy), launcher: launcher);

        bool opened = await coordinator.OpenCredentialHelpAsync();

        Assert.IsTrue(opened);
        // The default provider (KLIPY) wins over the provider that happens to be listed first.
        CollectionAssert.AreEqual(new[] { new Uri("https://partner.klipy.com/api-keys") }, launcher.LaunchRequests.ToArray());
    }

    [TestMethod]
    public async Task OpenCredentialHelpAsync_WhenNoProviderHasALink_OpensNothing()
    {
        FakeUriLauncherService launcher = new();
        OnboardingCoordinator coordinator = Create(
            new FakeProviderCatalog(new ProviderDescriptor { Id = "plain", DisplayName = "Plain" }),
            launcher: launcher);

        bool opened = await coordinator.OpenCredentialHelpAsync();

        Assert.IsFalse(opened);
        Assert.HasCount(0, launcher.LaunchRequests);
        Assert.IsNull(coordinator.CredentialHelpUri);
    }

    private static OnboardingCoordinator Create(
        FakeProviderCatalog catalog,
        ApiCredentialState? state = null,
        FakeUriLauncherService? launcher = null) =>
        new(
            new StubCredentialCoordinator(state ?? new ApiCredentialState
            {
                ProviderId = "klipy",
                ProviderDisplayName = "KLIPY",
                HasCredential = false
            }),
            launcher ?? new FakeUriLauncherService(),
            catalog);

    private sealed class StubCredentialCoordinator(ApiCredentialState state) : IApiCredentialCoordinator
    {
        public Task<ApiCredentialState> GetStateAsync(CancellationToken cancellationToken = default) => Task.FromResult(state);

        public Task<CredentialValidationResult> ValidateAndSaveAsync(string credential, CancellationToken cancellationToken = default) =>
            Task.FromResult(CredentialValidationResult.Valid());

        public Task DeleteAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
