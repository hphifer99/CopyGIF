using CopyGIF.Application.Providers;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Application.Tests.Providers;

[TestClass]
public sealed class ProviderCatalogTests
{
    [TestMethod]
    public void Constructor_MatchingRegistrations_CreatesCatalog()
    {
        FakeGifProvider provider =
            new(
                "klipy",
                "KLIPY");

        ProviderDescriptor descriptor =
            CreateDescriptor(
                "klipy",
                "KLIPY");

        ProviderCatalog catalog =
            new(
                [provider],
                [descriptor]);

        Assert.HasCount(
            1,
            catalog.Providers);

        Assert.AreSame(
            provider,
            catalog.GetRequiredProvider(
                "KLIPY"));
    }

    [TestMethod]
    public void Constructor_MultipleProviders_PreservesAllProviders()
    {
        FakeGifProvider klipy =
            new(
                "klipy",
                "KLIPY");

        FakeGifProvider futureProvider =
            new(
                "future",
                "Future Provider");

        ProviderCatalog catalog =
            new(
                [
                    klipy,
                    futureProvider
                ],
                [
                    CreateDescriptor(
                        "klipy",
                        "KLIPY"),

                    CreateDescriptor(
                        "future",
                        "Future Provider")
                ]);

        Assert.HasCount(
            2,
            catalog.Providers);

        Assert.AreSame(
            futureProvider,
            catalog.GetRequiredProvider(
                "future"));
    }

    [TestMethod]
    public void Constructor_DuplicateProviderId_Throws()
    {
        FakeGifProvider first =
            new(
                "klipy",
                "KLIPY");

        FakeGifProvider second =
            new(
                "KLIPY",
                "Duplicate");

        Assert.ThrowsExactly<InvalidOperationException>(
            () => new ProviderCatalog(
                [
                    first,
                    second
                ],
                [
                    CreateDescriptor(
                        "klipy",
                        "KLIPY")
                ]));
    }

    [TestMethod]
    public void Constructor_MissingDescriptor_Throws()
    {
        FakeGifProvider provider =
            new(
                "klipy",
                "KLIPY");

        Assert.ThrowsExactly<InvalidOperationException>(
            () => new ProviderCatalog(
                [provider],
                [
                    CreateDescriptor(
                        "different",
                        "Different")
                ]));
    }

    [TestMethod]
    public void TryGetProvider_UnknownProvider_ReturnsFalse()
    {
        ProviderCatalog catalog =
            new(
                [
                    new FakeGifProvider(
                        "klipy",
                        "KLIPY")
                ],
                [
                    CreateDescriptor(
                        "klipy",
                        "KLIPY")
                ]);

        bool found =
            catalog.TryGetProvider(
                "unknown",
                out IGifProvider? provider);

        Assert.IsFalse(
            found);

        Assert.IsNull(
            provider);
    }

    private static ProviderDescriptor
        CreateDescriptor(
            string id,
            string displayName)
    {
        return new ProviderDescriptor
        {
            Id = id,
            DisplayName = displayName,
            Capabilities =
                ProviderCapabilities.Search |
                ProviderCapabilities.Trending
        };
    }

    private sealed class FakeGifProvider :
        IGifProvider
    {
        public FakeGifProvider(
            string id,
            string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public string Id { get; }

        public string DisplayName { get; }

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
}
