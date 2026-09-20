using CopyGIF.Core.Models;

namespace CopyGIF.Core.Tests.Models;

[TestClass]
public sealed class BuiltInProvidersTests
{
    [TestMethod]
    public void All_ListsKlipyFirstAndGiphySecond()
    {
        CollectionAssert.AreEqual(
            new[] { "klipy", "giphy" },
            BuiltInProviders.All.Select(provider => provider.Id).ToArray());
    }

    [TestMethod]
    public void Klipy_ListsEveryMediaHostThatKlipyDocuments()
    {
        // From docs.klipy.com/network-requirements ("Media delivery"). A missing host would make
        // GIFs served from it fail to load, so this list is pinned by a test.
        CollectionAssert.AreEquivalent(
            new[] { "static.klipy.com", "static1.klipy.com", "static2.klipy.com" },
            BuiltInProviders.Klipy.MediaHosts.ToArray());
    }

    [TestMethod]
    public void Giphy_ListsTheKnownMediaHosts()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                "media.giphy.com", "media0.giphy.com", "media1.giphy.com",
                "media2.giphy.com", "media3.giphy.com", "media4.giphy.com"
            },
            BuiltInProviders.Giphy.MediaHosts.ToArray());
    }

    [TestMethod]
    public void BothProviders_KeepFavoritesAndRecents()
    {
        Assert.IsTrue(BuiltInProviders.Klipy.AllowsPersistentLibrary);
        Assert.IsTrue(BuiltInProviders.Giphy.AllowsPersistentLibrary);
    }

    [TestMethod]
    public void Giphy_LoadsPreviewsDirectly_AndKlipyDoesNot()
    {
        Assert.IsTrue(BuiltInProviders.Giphy.UsesDirectPreview);
        Assert.IsFalse(BuiltInProviders.Klipy.UsesDirectPreview);
    }

    [TestMethod]
    public void Providers_KeepTheBehaviorTheyHadBeforeItWasMovedIntoDescriptors()
    {
        // KLIPY keeps its first trending page and hides repeats. GIPHY does neither.
        Assert.IsTrue(BuiltInProviders.Klipy.CachesTrendingSnapshot);
        Assert.IsTrue(BuiltInProviders.Klipy.DeduplicatesResults);
        Assert.IsFalse(BuiltInProviders.Giphy.CachesTrendingSnapshot);
        Assert.IsFalse(BuiltInProviders.Giphy.DeduplicatesResults);
    }

    [TestMethod]
    public void EveryProvider_CarriesWhatSetupAndSettingsNeed()
    {
        foreach (ProviderDescriptor provider in BuiltInProviders.All)
        {
            Assert.IsTrue(provider.RequiresCredential, provider.Id);
            Assert.IsNotNull(provider.CredentialHelpUri, provider.Id);
            Assert.IsFalse(string.IsNullOrWhiteSpace(provider.AttributionText), provider.Id);
            Assert.IsNotEmpty(provider.MediaHosts, provider.Id);
        }
    }

    [TestMethod]
    public void Giphy_HasAnAttributionImageAndAnApiKeyHint()
    {
        Assert.AreEqual("Assets/PoweredByGiphy.png", BuiltInProviders.Giphy.AttributionImageAsset);
        Assert.IsNotNull(BuiltInProviders.Giphy.CredentialInstructions);
        Assert.IsNull(BuiltInProviders.Klipy.AttributionImageAsset);
    }

    [TestMethod]
    public void ANewDescriptor_IsCautiousByDefault()
    {
        ProviderDescriptor descriptor = new() { Id = "third", DisplayName = "Third" };

        // Keeping GIFs is a decision about a provider's terms, so it must be switched on.
        Assert.IsFalse(descriptor.AllowsPersistentLibrary);
        Assert.IsFalse(descriptor.UsesDirectPreview);
        Assert.IsFalse(descriptor.CachesTrendingSnapshot);
        Assert.IsTrue(descriptor.DeduplicatesResults);
        Assert.IsEmpty(descriptor.MediaHosts);
    }
}
