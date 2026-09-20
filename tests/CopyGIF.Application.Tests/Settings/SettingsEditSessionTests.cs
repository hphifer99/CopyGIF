using CopyGIF.Application.Settings;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Settings;

[TestClass]
public sealed class SettingsEditSessionTests
{
    [TestMethod]
    public async Task ApplyCommitsEveryCategoryAndPreservesLatestGeometry()
    {
        var h = new Harness();
        await h.Session.BeginAsync();
        h.Session.SetDraft(h.Session.Draft with
        {
            Search = new() { DebounceMilliseconds = 600 },
            Behavior = new() { CloseToTray = false },
            Providers = new() { ActiveProviderId = "giphy" }
        });
        h.Session.StageCredential("giphy", "new-key");
        h.Coordinator.Saved = h.Coordinator.Saved with { Window = new() { Width = 900, Left = 800 } };
        await h.Session.ApplyAsync();
        Assert.AreEqual(1, h.Coordinator.Saves);
        Assert.AreEqual(600, h.Coordinator.Saved.Search.DebounceMilliseconds);
        Assert.IsFalse(h.Coordinator.Saved.Behavior.CloseToTray);
        Assert.AreEqual("giphy", h.Coordinator.Saved.Providers.ActiveProviderId);
        Assert.AreEqual(900, h.Coordinator.Saved.Window.Width);
        Assert.AreEqual("new-key", await h.Secrets.GetAsync(SecretNames.GiphyApiKey));
        Assert.IsFalse(h.Session.HasChanges);
    }

    [TestMethod]
    public async Task FailedSettingsSaveRestoresBothKeysAndKeepsDraft()
    {
        var h = new Harness();
        await h.Secrets.SetAsync(SecretNames.KlipyApiKey, "original-klipy");
        await h.Secrets.SetAsync(SecretNames.GiphyApiKey, "original-giphy");
        await h.Session.BeginAsync();
        h.Session.StageCredential("klipy", "new-klipy");
        h.Session.StageCredential("giphy", "new-giphy");
        h.Coordinator.FailSave = true;
        await Assert.ThrowsExactlyAsync<IOException>(() => h.Session.ApplyAsync());
        Assert.AreEqual("original-klipy", await h.Secrets.GetAsync(SecretNames.KlipyApiKey));
        Assert.AreEqual("original-giphy", await h.Secrets.GetAsync(SecretNames.GiphyApiKey));
        Assert.IsTrue(h.Session.HasChanges);
        Assert.IsFalse(h.Session.IsApplying);
    }

    [TestMethod]
    public async Task InvalidSecondKeyDoesNotWriteFirstKey()
    {
        var h = new Harness();
        h.Giphy.ValidationHandler = (_, _) => Task.FromResult(CredentialValidationResult.Invalid("Rejected"));
        await h.Session.BeginAsync();
        h.Session.StageCredential("klipy", "first-key");
        h.Session.StageCredential("giphy", "bad-key");
        await Assert.ThrowsExactlyAsync<UserFacingException>(() => h.Session.ApplyAsync());
        Assert.IsNull(await h.Secrets.GetAsync(SecretNames.KlipyApiKey));
        Assert.AreEqual(0, h.Coordinator.Saves);
    }

    [TestMethod]
    public async Task UnchangedCredentialIsNotRevalidated()
    {
        var h = new Harness();
        await h.Secrets.SetAsync(SecretNames.GiphyApiKey, "unchanged");
        await h.Session.BeginAsync();
        h.Session.StageCredential("giphy", "unchanged");
        await h.Session.ApplyAsync();
        Assert.HasCount(0, h.Giphy.ValidationAttempts);
    }

    [TestMethod]
    public async Task DiscardRevertsPreviewWithoutSavingOrChangingCredentials()
    {
        var h = new Harness();
        await h.Session.BeginAsync();
        h.Session.SetDraft(h.Session.Draft with { Appearance = new() { Theme = AppTheme.Dark } });
        h.Session.StageCredential("giphy", "not-saved");
        Assert.AreEqual(AppTheme.Dark, (await h.Effective.LoadAsync()).Appearance.Theme);
        h.Session.Discard();
        Assert.AreEqual(AppTheme.System, (await h.Effective.LoadAsync()).Appearance.Theme);
        Assert.IsFalse(h.Effective.IsEditing);
        Assert.IsFalse(h.Session.HasChanges);
        Assert.AreEqual(0, h.Coordinator.Saves);
        Assert.IsNull(await h.Secrets.GetAsync(SecretNames.GiphyApiKey));
    }

    private static readonly ProviderDescriptor ThirdProvider = new()
    {
        Id = "third",
        DisplayName = "Third Gifs"
    };

    private static readonly ProviderDescriptor KeylessProvider = new()
    {
        Id = "free",
        DisplayName = "Free Gifs",
        RequiresCredential = false
    };

    [TestMethod]
    public void ProvidersListsOnlyProvidersThatNeedAKeyAndHaveACredentialManager()
    {
        var session = new SettingsEditSession(
            new Coordinator(), new Secrets(),
            [new FakeGifProviderCredentialManager(), new FakeGifProviderCredentialManager("third", "Third Gifs"),
             new FakeGifProviderCredentialManager("free", "Free Gifs")],
            new EffectiveSettings(new FakeSettingsStore()),
            new FakeProviderCatalog(BuiltInProviders.Klipy, BuiltInProviders.Giphy, ThirdProvider, KeylessProvider));

        // GIPHY has no credential manager here and Free Gifs needs no key. Order is the catalog order.
        CollectionAssert.AreEqual(
            new[] { "klipy", "third" },
            session.Providers.Select(provider => provider.Id).ToArray());
    }

    [TestMethod]
    public async Task AThirdProviderKeyIsKeptUnderThatProvidersOwnSecretName()
    {
        var secrets = new Secrets();
        var session = new SettingsEditSession(
            new Coordinator(), secrets,
            [new FakeGifProviderCredentialManager(), new FakeGifProviderCredentialManager("third", "Third Gifs", "custom.third.key")],
            new EffectiveSettings(new FakeSettingsStore()),
            new FakeProviderCatalog(BuiltInProviders.Klipy, ThirdProvider));
        await session.BeginAsync();

        session.StageCredential("third", "third-key");
        await session.ApplyAsync();

        Assert.AreEqual("third-key", await secrets.GetAsync("custom.third.key"));
        Assert.IsNull(await secrets.GetAsync("providers.third.apiKey"));
        Assert.IsNull(await secrets.GetAsync(SecretNames.KlipyApiKey));
    }

    [TestMethod]
    public async Task AFailedSaveRestoresAThirdProvidersKeyUnderItsOwnSecretName()
    {
        var secrets = new Secrets();
        await secrets.SetAsync("custom.third.key", "original-third");
        var coordinator = new Coordinator { FailSave = true };
        var session = new SettingsEditSession(
            coordinator, secrets,
            [new FakeGifProviderCredentialManager(), new FakeGifProviderCredentialManager("third", "Third Gifs", "custom.third.key")],
            new EffectiveSettings(new FakeSettingsStore()),
            new FakeProviderCatalog(BuiltInProviders.Klipy, ThirdProvider));
        await session.BeginAsync();

        session.StageCredential("third", "new-third");
        await Assert.ThrowsExactlyAsync<IOException>(() => session.ApplyAsync());

        Assert.AreEqual("original-third", await secrets.GetAsync("custom.third.key"));
        Assert.IsNull(await secrets.GetAsync("providers.third.apiKey"));
    }

    private sealed class Harness
    {
        public Coordinator Coordinator { get; } = new();
        public Secrets Secrets { get; } = new();
        public FakeGifProviderCredentialManager Giphy { get; } = new("giphy", "GIPHY");
        public EffectiveSettings Effective { get; } = new(new FakeSettingsStore());
        public SettingsEditSession Session { get; }
        public Harness() => Session = new(Coordinator, Secrets, [new FakeGifProviderCredentialManager(), Giphy], Effective, new FakeProviderCatalog());
    }
    private sealed class Coordinator : ISettingsCoordinator
    {
        public AppSettings Saved { get; set; } = new();
        public bool FailSave { get; set; }
        public int Saves { get; private set; }
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Saved);
        public Task<SettingsSaveResult> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            if (FailSave) throw new IOException("Save failed");
            Saved = settings; Saves++;
            return Task.FromResult(SettingsSaveResult.Success(settings));
        }
        public Task<SettingsSaveResult> RestoreDefaultsAsync(CancellationToken cancellationToken = default) => SaveAsync(new(), cancellationToken);
        public Task<SettingsSaveResult?> ChooseLibraryStorageRootAsync(CancellationToken cancellationToken = default) => Task.FromResult<SettingsSaveResult?>(null);
    }
    private sealed class Secrets : ISecretStore
    {
        private readonly Dictionary<string, string> _values = [];
        public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(_values.GetValueOrDefault(name));
        public Task SetAsync(string name, string value, CancellationToken cancellationToken = default) { _values[name] = value; return Task.CompletedTask; }
        public Task DeleteAsync(string name, CancellationToken cancellationToken = default) { _values.Remove(name); return Task.CompletedTask; }
    }
}
