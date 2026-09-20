using CopyGIF.Application.Onboarding;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Onboarding;

namespace CopyGIF.Presentation.Tests.Onboarding;

// The provider pick list in setup comes from the registered providers, so these tests use a
// third provider that the view model has never heard of.
[TestClass]
public sealed class OnboardingProviderChoiceTests
{
    private static readonly OnboardingProviderOption Klipy =
        new(
            "klipy",
            "KLIPY",
            new Uri("https://partner.klipy.com/api-keys"),
            null);

    private static readonly OnboardingProviderOption Giphy =
        new(
            "giphy",
            "GIPHY",
            new Uri("https://developers.giphy.com/dashboard/"),
            "For GIPHY, choose API instead of SDK.");

    private static readonly OnboardingProviderOption Third =
        new(
            "third",
            "Third Gifs",
            new Uri("https://third.example/keys"),
            "Create a read only key.");

    private static readonly OnboardingProviderOption ThirdWithoutHelp =
        new(
            "quiet",
            "Quiet Gifs",
            null,
            null);

    [TestMethod]
    public void Providers_MirrorTheRegisteredProviders_InOrder()
    {
        OnboardingViewModel viewModel =
            new(new FakeCoordinator(Klipy, Giphy, Third));

        CollectionAssert.AreEqual(
            new[] { "klipy", "giphy", "third" },
            viewModel.Providers.Select(choice => choice.Id).ToArray());

        CollectionAssert.AreEqual(
            new[] { "KLIPY", "GIPHY", "Third Gifs" },
            viewModel.Providers.Select(choice => choice.Name).ToArray());
    }

    [TestMethod]
    public void SelectedProviderId_StartsOnKlipy_WhenItIsRegistered()
    {
        OnboardingViewModel viewModel =
            new(new FakeCoordinator(Third, Giphy, Klipy));

        Assert.AreEqual("klipy", viewModel.SelectedProviderId);
    }

    [TestMethod]
    public void SelectedProviderId_StartsOnTheFirstProvider_WhenTheDefaultIsNotRegistered()
    {
        OnboardingViewModel viewModel =
            new(new FakeCoordinator(Third, Giphy));

        Assert.AreEqual("third", viewModel.SelectedProviderId);
    }

    [TestMethod]
    public void SelectedProviderId_ChangingIt_UpdatesNameHelpAndInstructions()
    {
        OnboardingViewModel viewModel =
            new(new FakeCoordinator(Klipy, Giphy, Third));

        viewModel.SelectedProviderId = "third";

        Assert.AreEqual("third", viewModel.ProviderId);
        Assert.AreEqual("Third Gifs", viewModel.ProviderDisplayName);
        Assert.AreEqual(Third.CredentialHelpUri, viewModel.CredentialHelpUri);
        Assert.AreEqual("Create a read only key.", viewModel.CredentialInstructions);
        Assert.IsTrue(viewModel.HasCredentialInstructions);
    }

    [TestMethod]
    public void SelectedProviderId_SwitchingBetweenProviders_ClearsInstructionsThatDoNotApply()
    {
        OnboardingViewModel viewModel =
            new(new FakeCoordinator(Klipy, Giphy, Third));

        viewModel.SelectedProviderId = "giphy";
        Assert.IsTrue(viewModel.HasCredentialInstructions);

        viewModel.SelectedProviderId = "klipy";

        Assert.IsNull(viewModel.CredentialInstructions);
        Assert.IsFalse(viewModel.HasCredentialInstructions);
        Assert.AreEqual(Klipy.CredentialHelpUri, viewModel.CredentialHelpUri);
    }

    [TestMethod]
    public void SelectedProviderId_ProviderWithoutAHelpPage_HasNoHelpUri()
    {
        OnboardingViewModel viewModel =
            new(new FakeCoordinator(Klipy, ThirdWithoutHelp));

        viewModel.SelectedProviderId = "quiet";

        Assert.AreEqual("Quiet Gifs", viewModel.ProviderDisplayName);
        Assert.IsNull(viewModel.CredentialHelpUri);
        Assert.IsFalse(viewModel.HasCredentialInstructions);
    }

    [TestMethod]
    public void SelectedProviderId_Changing_RaisesChangeNotificationsForTheDependentValues()
    {
        OnboardingViewModel viewModel =
            new(new FakeCoordinator(Klipy, Giphy));

        List<string?> changed = [];
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.SelectedProviderId = "giphy";

        Assert.Contains(nameof(OnboardingViewModel.ProviderDisplayName), changed);
        Assert.Contains(nameof(OnboardingViewModel.CredentialHelpUri), changed);
        Assert.Contains(nameof(OnboardingViewModel.CredentialInstructions), changed);
        Assert.Contains(nameof(OnboardingViewModel.HasCredentialInstructions), changed);
    }

    [TestMethod]
    public async Task LoadCommand_TakesInstructionsFromTheSetupState()
    {
        FakeCoordinator coordinator =
            new(Klipy, Giphy, Third)
            {
                State =
                    new OnboardingState
                    {
                        IsRequired = true,
                        ProviderId = "third",
                        ProviderDisplayName = "Third Gifs",
                        CredentialHelpUri = Third.CredentialHelpUri,
                        CredentialInstructions = Third.CredentialInstructions
                    }
            };

        OnboardingViewModel viewModel = new(coordinator);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.AreEqual("third", viewModel.SelectedProviderId);
        Assert.AreEqual("Create a read only key.", viewModel.CredentialInstructions);
        Assert.IsNotNull(viewModel.Message);
        Assert.Contains("Third Gifs", viewModel.Message.Text);
    }

    [TestMethod]
    public async Task CompleteCommand_SendsTheSelectedProviderIdToTheHandler()
    {
        FakeCoordinator coordinator = new(Klipy, Giphy, Third);
        OnboardingViewModel viewModel = new(coordinator);
        string? providerId = null;
        string? credential = null;

        viewModel.CompleteProvider =
            (id, key, _) =>
            {
                providerId = id;
                credential = key;
                return Task.FromResult(CredentialValidationResult.Valid());
            };

        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.SelectedProviderId = "third";
        viewModel.Credential = "  abc123  ";

        await viewModel.CompleteCommand.ExecuteAsync(null);

        Assert.AreEqual("third", providerId);
        Assert.AreEqual("abc123", credential);
        Assert.IsTrue(viewModel.IsCompleted);
        Assert.IsNotNull(viewModel.Message);
        Assert.Contains("Third Gifs", viewModel.Message.Text);
        // The default path, which knows only the default provider, was not used.
        Assert.IsNull(coordinator.LastCredential);
    }

    [TestMethod]
    public async Task OpenCredentialHelpCommand_OpensTheHelpPageOfTheSelectedProvider()
    {
        FakeCoordinator coordinator = new(Klipy, Giphy, Third);
        OnboardingViewModel viewModel = new(coordinator);
        Uri? opened = null;

        viewModel.OpenProviderHelp =
            (uri, _) =>
            {
                opened = uri;
                return Task.FromResult(true);
            };

        viewModel.SelectedProviderId = "third";

        await viewModel.OpenCredentialHelpCommand.ExecuteAsync(null);

        Assert.AreEqual(Third.CredentialHelpUri, opened);
        Assert.AreEqual(0, coordinator.OpenHelpCount);
    }

    private sealed class FakeCoordinator :
        IOnboardingCoordinator
    {
        public FakeCoordinator(
            params OnboardingProviderOption[] providers)
        {
            Providers = Array.AsReadOnly(providers);
        }

        public IReadOnlyList<OnboardingProviderOption> Providers { get; }

        public Uri? CredentialHelpUri =>
            Providers.FirstOrDefault()?.CredentialHelpUri;

        public OnboardingState State { get; init; } =
            new()
            {
                IsRequired = true,
                ProviderId = "klipy",
                ProviderDisplayName = "KLIPY",
                CredentialHelpUri = new Uri("https://partner.klipy.com/api-keys")
            };

        public string? LastCredential { get; private set; }

        public int OpenHelpCount { get; private set; }

        public Task<OnboardingState> GetStateAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(State);

        public Task<CredentialValidationResult> CompleteAsync(
            string credential,
            CancellationToken cancellationToken = default)
        {
            LastCredential = credential;
            return Task.FromResult(CredentialValidationResult.Valid());
        }

        public Task<bool> OpenCredentialHelpAsync(
            CancellationToken cancellationToken = default)
        {
            OpenHelpCount++;
            return Task.FromResult(true);
        }
    }
}
