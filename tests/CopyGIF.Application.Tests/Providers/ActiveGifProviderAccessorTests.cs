using CopyGIF.Application.Providers;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Tests.Providers;

[TestClass]
public sealed class ActiveGifProviderAccessorTests
{
    [TestMethod]
    public void GetActiveProvider_ConfiguredProvider_ReturnsConfiguredProvider()
    {
        FakeGifProvider klipy =
            new(
                "klipy");

        FakeGifProvider future =
            new(
                "future");

        ActiveGifProviderAccessor accessor =
            new(
                CreateCatalog(
                    klipy,
                    future));

        IGifProvider result =
            accessor.GetActiveProvider(
                new AppSettings
                {
                    Providers =
                        new ProviderSettings
                        {
                            ActiveProviderId =
                                "future"
                        }
                });

        Assert.AreSame(
            future,
            result);
    }

    [TestMethod]
    public void GetActiveProvider_UnknownProvider_FallsBackToKlipy()
    {
        FakeGifProvider klipy =
            new(
                "klipy");

        ActiveGifProviderAccessor accessor =
            new(
                CreateCatalog(
                    klipy));

        IGifProvider result =
            accessor.GetActiveProvider(
                new AppSettings
                {
                    Providers =
                        new ProviderSettings
                        {
                            ActiveProviderId =
                                "unavailable"
                        }
                });

        Assert.AreSame(
            klipy,
            result);
    }

    [TestMethod]
    public void GetActiveProvider_NoConfiguredOrDefaultProvider_Throws()
    {
        ActiveGifProviderAccessor accessor =
            new(
                CreateCatalog(
                    new FakeGifProvider(
                        "future")));

        Assert.ThrowsExactly<InvalidOperationException>(
            () => accessor.GetActiveProvider(
                new AppSettings()));
    }

    private static ProviderCatalog CreateCatalog(
        params FakeGifProvider[] providers)
    {
        ProviderDescriptor[] descriptors =
            providers
                .Select(
                    provider =>
                        new ProviderDescriptor
                        {
                            Id = provider.Id,
                            DisplayName =
                                provider.DisplayName,
                            Capabilities =
                                ProviderCapabilities.Search
                        })
                .ToArray();

        return new ProviderCatalog(
            providers,
            descriptors);
    }

    private sealed class FakeGifProvider :
        IGifProvider
    {
        public FakeGifProvider(
            string id)
        {
            Id = id;
        }

        public string Id { get; }

        public string DisplayName => Id;

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
