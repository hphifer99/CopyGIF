using CopyGIF.Application.Onboarding;
using CopyGIF.Application.Settings;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Testing;

namespace CopyGIF.Application.Tests.Onboarding;

[TestClass]
public sealed class ProviderOnboardingCoordinatorTests
{
    private const string ThirdSecretName = "providers.third-special.key";

    [TestMethod]
    public async Task CompleteAsync_SavesThroughTheChosenProvidersManager_AndMakesItActive()
    {
        Harness h = new();

        CredentialValidationResult result = await h.Coordinator.CompleteAsync("third", "third-key", CancellationToken.None);

        Assert.IsTrue(result.IsValid);
        CollectionAssert.AreEqual(new[] { "third-key" }, h.Third.SaveAttempts.ToArray());
        Assert.HasCount(0, h.Klipy.SaveAttempts);
        Assert.AreEqual("third-key", await h.Secrets.GetAsync(ThirdSecretName));
        Assert.AreEqual("third", h.Settings.Saved.Providers.ActiveProviderId);
    }

    [TestMethod]
    public async Task CompleteAsync_InvalidKey_ChangesNothing()
    {
        Harness h = new();
        h.Third.ValidationHandler = (_, _) => Task.FromResult(CredentialValidationResult.Invalid("Rejected"));

        CredentialValidationResult result = await h.Coordinator.CompleteAsync("third", "bad-key", CancellationToken.None);

        Assert.IsFalse(result.IsValid);
        Assert.HasCount(0, h.Third.SaveAttempts);
        Assert.AreEqual(0, h.Settings.Saves);
    }

    // The rollback must use the secret name of the provider that was being set up. It used to
    // choose between two built-in names, so any other provider would have restored the wrong key.
    [TestMethod]
    public async Task CompleteAsync_WhenSavingSettingsFails_RestoresTheProvidersOwnPreviousKey()
    {
        Harness h = new();
        await h.Secrets.SetAsync(ThirdSecretName, "third-old");
        await h.Secrets.SetAsync(SecretNames.KlipyApiKey, "klipy-untouched");
        h.Settings.FailWithMessage = "The settings file could not be written.";

        UserFacingException failure = await Assert.ThrowsExactlyAsync<UserFacingException>(
            () => h.Coordinator.CompleteAsync("third", "third-new", CancellationToken.None));

        Assert.AreEqual("The settings file could not be written.", failure.Message);
        Assert.AreEqual("third-old", await h.Secrets.GetAsync(ThirdSecretName));
        Assert.AreEqual("klipy-untouched", await h.Secrets.GetAsync(SecretNames.KlipyApiKey));
    }

    [TestMethod]
    public async Task CompleteAsync_WhenSavingSettingsFails_AndThereWasNoKey_RemovesTheNewOne()
    {
        Harness h = new();
        h.Settings.FailWithMessage = "nope";

        await Assert.ThrowsExactlyAsync<UserFacingException>(
            () => h.Coordinator.CompleteAsync("third", "third-new", CancellationToken.None));

        Assert.IsNull(await h.Secrets.GetAsync(ThirdSecretName));
    }

    private sealed class Harness
    {
        public Secrets Secrets { get; } = new();
        public Settings Settings { get; } = new();
        public FakeGifProviderCredentialManager Klipy { get; } = new();
        public FakeGifProviderCredentialManager Third { get; }
        public ProviderOnboardingCoordinator Coordinator { get; }

        public Harness()
        {
            Third = new("third", "Third", ThirdSecretName)
            {
            };
            // A real manager writes the key it is given, so the fake does the same.
            Third.SaveHandler = (credential, token) => Secrets.SetAsync(ThirdSecretName, credential, token);
            Coordinator = new ProviderOnboardingCoordinator(
                Settings,
                Secrets,
                [Klipy, Third],
                new FakeStartupService(),
                new FakeApplicationPaths());
        }
    }

    private sealed class Settings : ISettingsCoordinator
    {
        public AppSettings Saved { get; private set; } = new();
        public string? FailWithMessage { get; set; }
        public int Saves { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Saved);

        public Task<SettingsSaveResult> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            if (FailWithMessage is not null)
            {
                return Task.FromResult(new SettingsSaveResult
                {
                    Succeeded = false,
                    EffectiveSettings = Saved,
                    ErrorMessage = FailWithMessage
                });
            }

            Saved = settings;
            Saves++;
            return Task.FromResult(SettingsSaveResult.Success(settings));
        }

        public Task<SettingsSaveResult> RestoreDefaultsAsync(CancellationToken cancellationToken = default) => SaveAsync(new(), cancellationToken);

        public Task<SettingsSaveResult?> ChooseLibraryStorageRootAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<SettingsSaveResult?>(null);
    }

    private sealed class Secrets : ISecretStore
    {
        private readonly Dictionary<string, string> _values = [];

        public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(_values.GetValueOrDefault(name));

        public Task SetAsync(string name, string value, CancellationToken cancellationToken = default) { _values[name] = value; return Task.CompletedTask; }

        public Task DeleteAsync(string name, CancellationToken cancellationToken = default) { _values.Remove(name); return Task.CompletedTask; }
    }
}
