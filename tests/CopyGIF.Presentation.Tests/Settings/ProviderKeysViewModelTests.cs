using CopyGIF.Application.Settings;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Settings;
using CopyGIF.Presentation.Tests.Common;

namespace CopyGIF.Presentation.Tests.Settings;

// The API key section of Settings has one entry for every registered provider that needs a key.
// These tests use a third provider that the view model has never heard of.
[TestClass]
public sealed class ProviderKeysViewModelTests
{
    private static readonly ProviderDescriptor Third = new()
    {
        Id = "third",
        DisplayName = "Third Gifs",
        CredentialHelpUri = new Uri("https://third.example/keys"),
        CredentialInstructions = "Create a read only key."
    };

    private static readonly ProviderDescriptor NoKeyNeeded = new()
    {
        Id = "free",
        DisplayName = "Free Gifs",
        RequiresCredential = false
    };

    [TestMethod]
    public void Keys_HaveOneEntryPerProviderThatNeedsAKey_InRegistrationOrder()
    {
        Fixture fixture = new(BuiltInProviders.Klipy, BuiltInProviders.Giphy, Third);

        ProviderKeysViewModel viewModel = new(fixture.Session);

        CollectionAssert.AreEqual(
            new[] { "klipy", "giphy", "third" },
            viewModel.Keys.Select(entry => entry.Id).ToArray());

        CollectionAssert.AreEqual(
            new[] { "KLIPY", "GIPHY", "Third Gifs" },
            viewModel.Providers.Select(choice => choice.Name).ToArray());
    }

    [TestMethod]
    public void Keys_LeaveOutAProviderThatNeedsNoKey_AndOneWithoutACredentialManager()
    {
        Fixture fixture = new(
            managed: [BuiltInProviders.Klipy, Third, NoKeyNeeded],
            catalog: [BuiltInProviders.Klipy, BuiltInProviders.Giphy, Third, NoKeyNeeded]);

        ProviderKeysViewModel viewModel = new(fixture.Session);

        // GIPHY has no credential manager here, and Free Gifs needs no key.
        CollectionAssert.AreEqual(
            new[] { "klipy", "third" },
            viewModel.Keys.Select(entry => entry.Id).ToArray());
    }

    [TestMethod]
    public void Entry_TextsAndLinksComeFromTheProviderDescriptor()
    {
        Fixture fixture = new(BuiltInProviders.Klipy, Third);
        ProviderKeysViewModel viewModel = new(fixture.Session);

        ProviderKeyEntry third = viewModel.Find("third")!;

        Assert.AreEqual("Third Gifs API key", third.KeyHeader);
        Assert.AreEqual("Remove saved Third Gifs key on Apply", third.RemoveLabel);
        Assert.AreEqual("Open Third Gifs API key help", third.HelpLabel);
        Assert.AreEqual(Third.CredentialHelpUri, third.HelpUri);
        Assert.IsTrue(third.HasHelpUri);
        Assert.AreEqual("Create a read only key.", third.Instructions);
        Assert.IsTrue(third.HasInstructions);

        ProviderKeyEntry klipy = viewModel.Find("klipy")!;

        Assert.IsTrue(klipy.HasHelpUri);
        Assert.IsFalse(klipy.HasInstructions);
    }

    [TestMethod]
    public void Find_IgnoresCase_AndReturnsNullForAnUnknownProvider()
    {
        Fixture fixture = new(BuiltInProviders.Klipy, Third);
        ProviderKeysViewModel viewModel = new(fixture.Session);

        Assert.AreSame(viewModel.Keys[1], viewModel.Find("THIRD"));
        Assert.IsNull(viewModel.Find("missing"));
    }

    [TestMethod]
    public async Task RefreshAsync_ReadsWhichProvidersHaveASavedKey()
    {
        Fixture fixture = new(BuiltInProviders.Klipy, BuiltInProviders.Giphy, Third);
        await fixture.Secrets.SetAsync("providers.third.apiKey", "saved");
        await fixture.Session.BeginAsync();
        ProviderKeysViewModel viewModel = new(fixture.Session);

        await viewModel.RefreshAsync();

        Assert.IsFalse(viewModel.Find("klipy")!.HasSavedKey);
        Assert.IsFalse(viewModel.Find("giphy")!.HasSavedKey);
        Assert.IsTrue(viewModel.Find("third")!.HasSavedKey);
        Assert.AreEqual("A Third Gifs key is saved.", viewModel.Find("third")!.Status);
        Assert.AreEqual("No KLIPY key is saved.", viewModel.Find("klipy")!.Status);
        Assert.AreEqual("Enter API key", viewModel.Find("klipy")!.Placeholder);
        Assert.AreNotEqual("Enter API key", viewModel.Find("third")!.Placeholder);
    }

    [TestMethod]
    public async Task RefreshAsync_TakesTheActiveProviderFromTheSavedSettings_AndStagesNothing()
    {
        Fixture fixture = new(BuiltInProviders.Klipy, BuiltInProviders.Giphy);
        fixture.Coordinator.Saved = new AppSettings
        {
            Providers = new ProviderSettings { ActiveProviderId = "giphy" }
        };
        await fixture.Session.BeginAsync();
        ProviderKeysViewModel viewModel = new(fixture.Session);

        await viewModel.RefreshAsync();

        Assert.AreEqual("giphy", viewModel.ActiveProviderId);
        Assert.IsFalse(fixture.Session.HasChanges);
    }

    [TestMethod]
    public async Task RefreshAsync_AfterApply_ClearsTheTypedKeyAndShowsTheKeyAsSaved()
    {
        Fixture fixture = new(BuiltInProviders.Klipy, Third);
        await fixture.Session.BeginAsync();
        ProviderKeysViewModel viewModel = new(fixture.Session);
        await viewModel.RefreshAsync();

        viewModel.Find("third")!.Credential = "new-key";
        await fixture.Session.ApplyAsync();
        await viewModel.RefreshAsync();

        Assert.AreEqual(string.Empty, viewModel.Find("third")!.Credential);
        Assert.IsTrue(viewModel.Find("third")!.HasSavedKey);
        Assert.IsFalse(fixture.Session.HasChanges);
    }

    [TestMethod]
    public async Task TypingAKey_StagesIt_AndClearingItUnstagesIt()
    {
        Fixture fixture = new(BuiltInProviders.Klipy, Third);
        await fixture.Session.BeginAsync();
        ProviderKeysViewModel viewModel = new(fixture.Session);
        await viewModel.RefreshAsync();

        viewModel.Find("third")!.Credential = "new-key";
        Assert.IsTrue(fixture.Session.HasChanges);

        viewModel.Find("third")!.Credential = "   ";
        Assert.IsFalse(fixture.Session.HasChanges);
    }

    [TestMethod]
    public async Task Apply_WritesTheThirdProvidersKeyUnderItsOwnSecretName_AndLeavesOthersAlone()
    {
        Fixture fixture = new(BuiltInProviders.Klipy, BuiltInProviders.Giphy, Third);
        await fixture.Secrets.SetAsync("providers.klipy.apiKey", "klipy-old");
        await fixture.Session.BeginAsync();
        ProviderKeysViewModel viewModel = new(fixture.Session);
        await viewModel.RefreshAsync();

        viewModel.Find("third")!.Credential = "  third-key  ";
        await fixture.Session.ApplyAsync();

        Assert.AreEqual("third-key", await fixture.Secrets.GetAsync("providers.third.apiKey"));
        Assert.AreEqual("klipy-old", await fixture.Secrets.GetAsync("providers.klipy.apiKey"));
        Assert.IsNull(await fixture.Secrets.GetAsync("providers.giphy.apiKey"));
    }

    [TestMethod]
    public async Task DeleteKey_StagesARemoval_ThatApplyCarriesOut()
    {
        Fixture fixture = new(BuiltInProviders.Klipy, Third);
        await fixture.Secrets.SetAsync("providers.third.apiKey", "saved");
        await fixture.Session.BeginAsync();
        ProviderKeysViewModel viewModel = new(fixture.Session);
        await viewModel.RefreshAsync();

        viewModel.Find("third")!.DeleteKey = true;

        Assert.IsTrue(fixture.Session.HasChanges);
        Assert.AreEqual("Enter API key", viewModel.Find("third")!.Placeholder);

        await fixture.Session.ApplyAsync();

        Assert.IsNull(await fixture.Secrets.GetAsync("providers.third.apiKey"));
    }

    [TestMethod]
    public async Task Discard_LeavesTheSavedKeysAlone()
    {
        Fixture fixture = new(BuiltInProviders.Klipy, Third);
        await fixture.Secrets.SetAsync("providers.third.apiKey", "saved");
        await fixture.Session.BeginAsync();
        ProviderKeysViewModel viewModel = new(fixture.Session);
        await viewModel.RefreshAsync();

        viewModel.Find("third")!.Credential = "typed";
        fixture.Session.Discard();
        await viewModel.RefreshAsync();

        Assert.IsFalse(fixture.Session.HasChanges);
        Assert.AreEqual("saved", await fixture.Secrets.GetAsync("providers.third.apiKey"));
        Assert.AreEqual(string.Empty, viewModel.Find("third")!.Credential);
    }

    [TestMethod]
    public void EditingAnyKey_RaisesAChangeNotificationForTheWholeSection()
    {
        Fixture fixture = new(BuiltInProviders.Klipy, Third);
        ProviderKeysViewModel viewModel = new(fixture.Session);
        List<string?> changed = [];
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Find("third")!.Credential = "abc";

        Assert.Contains(nameof(ProviderKeysViewModel.Keys), changed);
    }

    [TestMethod]
    public void EntryChanges_RaiseNotificationsForTheTextsThatDependOnThem()
    {
        Fixture fixture = new(BuiltInProviders.Klipy, Third);
        ProviderKeysViewModel viewModel = new(fixture.Session);
        ProviderKeyEntry entry = viewModel.Find("third")!;
        List<string?> changed = [];
        entry.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        entry.Credential = "abc";
        entry.DeleteKey = true;

        Assert.Contains(nameof(ProviderKeyEntry.Placeholder), changed);
        Assert.Contains(nameof(ProviderKeyEntry.Credential), changed);
        Assert.Contains(nameof(ProviderKeyEntry.DeleteKey), changed);
    }

    private sealed class Fixture
    {
        public Fixture(params ProviderDescriptor[] providers)
            : this(providers, providers)
        {
        }

        public Fixture(
            ProviderDescriptor[] managed,
            ProviderDescriptor[] catalog)
        {
            Secrets = new MemorySecretStore();
            Session = new SettingsEditSession(
                Coordinator,
                Secrets,
                managed.Select(descriptor => (IGifProviderCredentialManager)new Manager(descriptor, Secrets)).ToArray(),
                new EffectiveSettings(new Store()),
                new FakeProviderCatalog(catalog));
        }

        public Coordinator Coordinator { get; } = new();

        public MemorySecretStore Secrets { get; }

        public SettingsEditSession Session { get; }
    }

    private sealed class Manager(
        ProviderDescriptor descriptor,
        MemorySecretStore secrets) :
        IGifProviderCredentialManager
    {
        // Every provider keeps its key under its own name, for example providers.third.apiKey.
        public string ProviderId => descriptor.Id;

        public string DisplayName => descriptor.DisplayName;

        public string SecretName => $"providers.{descriptor.Id}.apiKey";

        // A saved key is a value in the shared secret store under this provider's secret name.
        public Task<bool> HasCredentialAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(secrets.Contains(SecretName));

        public Task<CredentialValidationResult> ValidateCredentialAsync(
            string credential,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CredentialValidationResult.Valid());

        public Task SaveCredentialAsync(string credential, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteCredentialAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class Coordinator :
        ISettingsCoordinator
    {
        public AppSettings Saved { get; set; } = new();

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Saved);

        public Task<SettingsSaveResult> SaveAsync(
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            Saved = settings;
            return Task.FromResult(SettingsSaveResult.Success(settings));
        }

        public Task<SettingsSaveResult> RestoreDefaultsAsync(CancellationToken cancellationToken = default) =>
            SaveAsync(new AppSettings(), cancellationToken);

        public Task<SettingsSaveResult?> ChooseLibraryStorageRootAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SettingsSaveResult?>(null);
    }

    private sealed class Store :
        ISettingsStore
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppSettings());

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class MemorySecretStore :
        ISecretStore
    {
        private readonly Dictionary<string, string> _values = [];

        public bool Contains(string name) => _values.ContainsKey(name);

        public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.GetValueOrDefault(name));

        public Task SetAsync(string name, string value, CancellationToken cancellationToken = default)
        {
            _values[name] = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            _values.Remove(name);
            return Task.CompletedTask;
        }
    }
}
