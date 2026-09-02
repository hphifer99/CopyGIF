using System.Net;
using CopyGIF.Core.Models;
using CopyGIF.Infrastructure.Media;

namespace CopyGIF.Infrastructure.Tests.Media;

[TestClass]
public sealed class MediaHostPolicyTests
{
    [TestMethod]
    public async Task ValidateAsync_ApprovedPublicHost_Succeeds()
    {
        FakeHostAddressResolver resolver =
            CreateResolver(
                IPAddress.Parse(
                    "93.184.216.34"));

        MediaHostPolicy policy =
            CreatePolicy(
                resolver);

        await policy.ValidateAsync(
            new Uri(
                "https://static.klipy.com/image.gif"));

        CollectionAssert.AreEqual(
            new[]
            {
                "static.klipy.com"
            },
            resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ValidateAsync_HttpUrl_IsRejected()
    {
        FakeHostAddressResolver resolver =
            CreateResolver(
                IPAddress.Parse(
                    "93.184.216.34"));

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => CreatePolicy(
                        resolver)
                    .ValidateAsync(
                        new Uri(
                            "http://static.klipy.com/image.gif")));

        Assert.AreEqual(
            MediaDownloadFailure.InvalidUri,
            exception.Failure);

        Assert.IsEmpty(
            resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ValidateAsync_UnapprovedHost_IsRejectedBeforeDns()
    {
        FakeHostAddressResolver resolver =
            new();

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => CreatePolicy(
                        resolver)
                    .ValidateAsync(
                        new Uri(
                            "https://example.com/image.gif")));

        Assert.AreEqual(
            MediaDownloadFailure.UnapprovedHost,
            exception.Failure);

        Assert.IsEmpty(
            resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ValidateAsync_PrivateIpv4_IsRejected()
    {
        string[] privateAddresses =
        [
            "127.0.0.1",
            "10.1.2.3",
            "172.16.0.1",
            "192.168.1.1",
            "169.254.10.20",
            "100.64.0.1"
        ];

        foreach (string address
                 in privateAddresses)
        {
            FakeHostAddressResolver resolver =
                CreateResolver(
                    IPAddress.Parse(
                        address));

            MediaDownloadException exception =
                await Assert.ThrowsAsync<
                    MediaDownloadException>(
                    () => CreatePolicy(
                            resolver)
                        .ValidateAsync(
                            new Uri(
                                "https://static.klipy.com/image.gif")));

            Assert.AreEqual(
                MediaDownloadFailure.PrivateNetworkTarget,
                exception.Failure,
                $"Address was not rejected: {address}");
        }
    }

    [TestMethod]
    public async Task ValidateAsync_PrivateIpv6_IsRejected()
    {
        string[] privateAddresses =
        [
            "::1",
            "fc00::1",
            "fe80::1",
            "2001:db8::1"
        ];

        foreach (string address
                 in privateAddresses)
        {
            FakeHostAddressResolver resolver =
                CreateResolver(
                    IPAddress.Parse(
                        address));

            MediaDownloadException exception =
                await Assert.ThrowsAsync<
                    MediaDownloadException>(
                    () => CreatePolicy(
                            resolver)
                        .ValidateAsync(
                            new Uri(
                                "https://static.klipy.com/image.gif")));

            Assert.AreEqual(
                MediaDownloadFailure.PrivateNetworkTarget,
                exception.Failure,
                $"Address was not rejected: {address}");
        }
    }

    [TestMethod]
    public async Task ValidateAsync_MixedPublicAndPrivateAnswers_IsRejected()
    {
        FakeHostAddressResolver resolver =
            CreateResolver(
                IPAddress.Parse(
                    "93.184.216.34"),
                IPAddress.Parse(
                    "127.0.0.1"));

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => CreatePolicy(
                        resolver)
                    .ValidateAsync(
                        new Uri(
                            "https://static.klipy.com/image.gif")));

        Assert.AreEqual(
            MediaDownloadFailure.PrivateNetworkTarget,
            exception.Failure);
    }

    [TestMethod]
    public async Task ValidateAsync_EmptyDnsAnswer_IsRejected()
    {
        FakeHostAddressResolver resolver =
            new();

        MediaDownloadException exception =
            await Assert.ThrowsAsync<
                MediaDownloadException>(
                () => CreatePolicy(
                        resolver)
                    .ValidateAsync(
                        new Uri(
                            "https://static.klipy.com/image.gif")));

        Assert.AreEqual(
            MediaDownloadFailure.HostResolutionFailed,
            exception.Failure);
    }

    private static FakeHostAddressResolver
        CreateResolver(
            params IPAddress[] addresses)
    {
        FakeHostAddressResolver resolver =
            new();

        resolver.Add(
            "static.klipy.com",
            addresses);

        return resolver;
    }

    private static MediaHostPolicy CreatePolicy(
        IHostAddressResolver resolver)
    {
        return new MediaHostPolicy(
            resolver,
            [
                "static.klipy.com"
            ]);
    }
}

internal sealed class FakeHostAddressResolver :
    IHostAddressResolver
{
    private readonly Dictionary<
        string,
        IPAddress[]> _addresses =
            new(
                StringComparer.OrdinalIgnoreCase);

    public List<string> ResolvedHosts
    {
        get;
    } = [];

    public void Add(
        string host,
        params IPAddress[] addresses)
    {
        _addresses[host] = addresses;
    }

    public Task<IPAddress[]> ResolveAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        cancellationToken
            .ThrowIfCancellationRequested();

        ResolvedHosts.Add(
            host);

        _addresses.TryGetValue(
            host,
            out IPAddress[]? addresses);

        return Task.FromResult(
            addresses ?? []);
    }
}
