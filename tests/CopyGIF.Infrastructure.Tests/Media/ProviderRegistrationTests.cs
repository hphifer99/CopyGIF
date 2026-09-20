using System.Net;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;
using CopyGIF.Infrastructure.Media;
using CopyGIF.Infrastructure.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;

namespace CopyGIF.Infrastructure.Tests.Media;

// These tests check how the providers are registered: the descriptors that describe them, the
// media hosts derived from those descriptors, and the way a further provider joins the app.
// The last test reads the process-wide repair log sink, so the class must not run in parallel.
[TestClass]
[DoNotParallelize]
public sealed class ProviderRegistrationTests
{
    private static readonly ProviderDescriptor Third = new()
    {
        Id = "third",
        DisplayName = "Third Gifs",
        MediaHosts = ["cdn.third.example"]
    };

    [TestMethod]
    public void AddCopyGifInfrastructure_RegistersBothBuiltInDescriptors_InOrder()
    {
        using ServiceProvider provider = BuildProvider();

        string[] ids = provider
            .GetServices<ProviderDescriptor>()
            .Select(descriptor => descriptor.Id)
            .ToArray();

        CollectionAssert.AreEqual(
            new[] { BuiltInProviders.KlipyId, BuiltInProviders.GiphyId },
            ids);
    }

    [TestMethod]
    public void AddCopyGifInfrastructure_EveryDescriptor_HasAProviderAndACredentialManager()
    {
        using ServiceProvider provider = BuildProvider();

        string[] descriptorIds = provider
            .GetServices<ProviderDescriptor>()
            .Select(descriptor => descriptor.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        string[] providerIds = provider
            .GetServices<IGifProvider>()
            .Select(gifProvider => gifProvider.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        string[] managerIds = provider
            .GetServices<IGifProviderCredentialManager>()
            .Select(manager => manager.ProviderId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(descriptorIds, providerIds);
        CollectionAssert.AreEqual(descriptorIds, managerIds);
    }

    [TestMethod]
    public void AddCopyGifInfrastructure_CredentialManagers_UseDistinctStableSecretNames()
    {
        using ServiceProvider provider = BuildProvider();

        IGifProviderCredentialManager[] managers = provider
            .GetServices<IGifProviderCredentialManager>()
            .ToArray();

        Assert.AreEqual(
            managers.Length,
            managers.Select(manager => manager.SecretName).Distinct(StringComparer.Ordinal).Count());

        // A saved key is found again after an update only if its name never changes.
        Assert.AreEqual(
            "providers.klipy.apiKey",
            managers.Single(manager => manager.ProviderId == BuiltInProviders.KlipyId).SecretName);
        Assert.AreEqual(
            "providers.giphy.apiKey",
            managers.Single(manager => manager.ProviderId == BuiltInProviders.GiphyId).SecretName);
    }

    [TestMethod]
    public void AddCopyGifInfrastructure_DescriptorsAndProviderClassesAgreeOnNames()
    {
        using ServiceProvider provider = BuildProvider();

        Dictionary<string, ProviderDescriptor> descriptors = provider
            .GetServices<ProviderDescriptor>()
            .ToDictionary(descriptor => descriptor.Id, StringComparer.Ordinal);

        foreach (IGifProvider gifProvider in provider.GetServices<IGifProvider>())
        {
            Assert.AreEqual(
                descriptors[gifProvider.Id].DisplayName,
                gifProvider.DisplayName,
                $"The display name of {gifProvider.Id} differs between its descriptor and its provider.");
        }

        foreach (IGifProviderCredentialManager manager in provider.GetServices<IGifProviderCredentialManager>())
        {
            Assert.AreEqual(
                descriptors[manager.ProviderId].DisplayName,
                manager.DisplayName,
                $"The display name of {manager.ProviderId} differs between its descriptor and its credential manager.");
        }
    }

    [TestMethod]
    public async Task MediaHostPolicy_ApprovesEveryHostThatKlipyDocumentsAndEveryGiphyHost()
    {
        string[] hosts =
        [
            "static.klipy.com", "static1.klipy.com", "static2.klipy.com",
            "media.giphy.com", "media0.giphy.com", "media1.giphy.com",
            "media2.giphy.com", "media3.giphy.com", "media4.giphy.com"
        ];

        FakeHostAddressResolver resolver = PublicResolver(hosts);

        using ServiceProvider provider = BuildProvider(resolver);
        MediaHostPolicy policy = provider.GetRequiredService<MediaHostPolicy>();

        foreach (string host in hosts)
        {
            await policy.ValidateAsync(new Uri($"https://{host}/a/b.gif"));
        }

        CollectionAssert.AreEqual(hosts, resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task MediaHostPolicy_RejectsAHostNoProviderLists()
    {
        FakeHostAddressResolver resolver = PublicResolver("static3.klipy.com", "media5.giphy.com");

        using ServiceProvider provider = BuildProvider(resolver);
        MediaHostPolicy policy = provider.GetRequiredService<MediaHostPolicy>();

        foreach (string host in new[] { "static3.klipy.com", "media5.giphy.com", "example.com" })
        {
            MediaDownloadException exception = await Assert.ThrowsAsync<MediaDownloadException>(
                () => policy.ValidateAsync(new Uri($"https://{host}/a.gif")));

            Assert.AreEqual(MediaDownloadFailure.UnapprovedHost, exception.Failure, host);
        }

        Assert.IsEmpty(resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task MediaHostPolicy_ApprovesTheHostsOfAThirdProvider_WithoutAnyOtherChange()
    {
        FakeHostAddressResolver resolver = PublicResolver("cdn.third.example", "static.klipy.com");

        ServiceCollection services = NewServices(resolver);
        services.AddSingleton(Third);
        using ServiceProvider provider = services.BuildServiceProvider();

        MediaHostPolicy policy = provider.GetRequiredService<MediaHostPolicy>();

        await policy.ValidateAsync(new Uri("https://cdn.third.example/a.gif"));
        // The built-in hosts are still approved next to it.
        await policy.ValidateAsync(new Uri("https://static.klipy.com/a.gif"));

        CollectionAssert.AreEqual(
            new[] { "cdn.third.example", "static.klipy.com" },
            resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task MediaHostPolicy_WithoutAThirdProvider_RejectsItsHost()
    {
        FakeHostAddressResolver resolver = PublicResolver("cdn.third.example");

        using ServiceProvider provider = BuildProvider(resolver);
        MediaHostPolicy policy = provider.GetRequiredService<MediaHostPolicy>();

        MediaDownloadException exception = await Assert.ThrowsAsync<MediaDownloadException>(
            () => policy.ValidateAsync(new Uri("https://cdn.third.example/a.gif")));

        Assert.AreEqual(MediaDownloadFailure.UnapprovedHost, exception.Failure);
    }

    [TestMethod]
    public async Task MediaHostPolicy_RejectedHost_LeavesATraceWithOnlyTheHostName()
    {
        FakeHostAddressResolver resolver = PublicResolver();
        List<string> lines = [];
        Action<string>? previous = RepairDiagnostics.Sink;

        try
        {
            RepairDiagnostics.Sink = lines.Add;

            using ServiceProvider provider = BuildProvider(resolver);
            MediaHostPolicy policy = provider.GetRequiredService<MediaHostPolicy>();

            await Assert.ThrowsAsync<MediaDownloadException>(
                () => policy.ValidateAsync(new Uri("https://static3.klipy.com/secret/path.gif?token=abc")));
        }
        finally
        {
            RepairDiagnostics.Sink = previous;
        }

        string line = lines.Single(text => text.Contains("media-host-rejected", StringComparison.Ordinal));

        Assert.Contains("static3.klipy.com", line);
        Assert.DoesNotContain("secret", line);
        Assert.DoesNotContain("token", line);
    }

    [TestMethod]
    public void BuiltInProviders_GiphyDirectHostsAreExactlyItsMediaHosts()
    {
        // The picker may only load a preview directly from a host the downloader would also allow.
        foreach (string host in BuiltInProviders.Giphy.MediaHosts)
        {
            Assert.IsTrue(
                ProviderMediaPolicy.IsDirectMediaUri(new Uri($"https://{host}/a.gif")),
                host);
        }
    }

    private static ServiceProvider BuildProvider(IHostAddressResolver? resolver = null) =>
        NewServices(resolver).BuildServiceProvider();

    private static ServiceCollection NewServices(IHostAddressResolver? resolver)
    {
        ServiceCollection services = new();
        services.AddCopyGifInfrastructure();
        services.AddSingleton<ISecretStore>(new TestSecretStore());

        if (resolver is not null)
        {
            // The last registration wins, so this replaces the real DNS resolver.
            services.AddSingleton<IHostAddressResolver>(resolver);
        }

        return services;
    }

    private static FakeHostAddressResolver PublicResolver(params string[] hosts)
    {
        FakeHostAddressResolver resolver = new();

        foreach (string host in hosts)
        {
            resolver.Add(host, IPAddress.Parse("93.184.216.34"));
        }

        return resolver;
    }
}
