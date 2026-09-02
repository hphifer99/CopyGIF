using System.Net;
using System.Net.Sockets;
using CopyGIF.Core.Models;

namespace CopyGIF.Infrastructure.Media;

public interface IHostAddressResolver
{
    Task<IPAddress[]> ResolveAsync(
        string host,
        CancellationToken cancellationToken = default);
}

public sealed class SystemHostAddressResolver :
    IHostAddressResolver
{
    public Task<IPAddress[]> ResolveAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            host);

        return Dns.GetHostAddressesAsync(
            host,
            cancellationToken);
    }
}

public sealed class MediaHostPolicy
{
    private readonly IHostAddressResolver
        _addressResolver;

    private readonly HashSet<string>
        _approvedHosts;

    public MediaHostPolicy(
        IHostAddressResolver addressResolver,
        IEnumerable<string> approvedHosts)
    {
        _addressResolver =
            addressResolver ??
            throw new ArgumentNullException(
                nameof(addressResolver));

        ArgumentNullException.ThrowIfNull(
            approvedHosts);

        _approvedHosts =
            new HashSet<string>(
                approvedHosts
                    .Select(
                        NormalizeHost),
                StringComparer.OrdinalIgnoreCase);

        if (_approvedHosts.Count == 0)
        {
            throw new ArgumentException(
                "At least one approved media host is required.",
                nameof(approvedHosts));
        }
    }

    public async Task ValidateAsync(
        Uri uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            uri);

        if (!uri.IsAbsoluteUri ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            (!uri.IsDefaultPort && uri.Port != 443) ||
            Uri.CheckHostName(uri.IdnHost) !=
                UriHostNameType.Dns)
        {
            throw new MediaDownloadException(
                MediaDownloadFailure.InvalidUri,
                "Media URLs must be absolute HTTPS URLs without credentials, fragments, or nonstandard ports.");
        }

        string host =
            NormalizeHost(
                uri.IdnHost);

        if (!_approvedHosts.Contains(
                host))
        {
            throw new MediaDownloadException(
                MediaDownloadFailure.UnapprovedHost,
                $"The media host '{host}' is not approved.");
        }

        IPAddress[] addresses;

        try
        {
            addresses =
                await _addressResolver
                    .ResolveAsync(
                        host,
                        cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SocketException exception)
        {
            throw new MediaDownloadException(
                MediaDownloadFailure.HostResolutionFailed,
                $"The media host '{host}' could not be resolved.",
                exception);
        }

        if (addresses.Length == 0)
        {
            throw new MediaDownloadException(
                MediaDownloadFailure.HostResolutionFailed,
                $"The media host '{host}' did not resolve to an address.");
        }

        if (addresses.Any(
                IsNonPublicAddress))
        {
            throw new MediaDownloadException(
                MediaDownloadFailure.PrivateNetworkTarget,
                $"The media host '{host}' resolved to a nonpublic address.");
        }
    }

    private static string NormalizeHost(
        string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            host);

        return host
            .Trim()
            .TrimEnd('.')
            .ToLowerInvariant();
    }

    private static bool IsNonPublicAddress(
        IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(
            address);

        if (address.IsIPv4MappedToIPv6)
        {
            return IsNonPublicAddress(
                address.MapToIPv4());
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) ||
            address.Equals(IPAddress.IPv6None))
        {
            return true;
        }

        byte[] bytes =
            address.GetAddressBytes();

        if (bytes.Length == 4)
        {
            return IsNonPublicIpv4(
                bytes);
        }

        if (bytes.Length == 16)
        {
            return IsNonPublicIpv6(
                bytes);
        }

        return true;
    }

    private static bool IsNonPublicIpv4(
        byte[] bytes)
    {
        return
            bytes[0] == 0 ||
            bytes[0] == 10 ||
            bytes[0] == 127 ||
            (bytes[0] == 100 &&
             bytes[1] is >= 64 and <= 127) ||
            (bytes[0] == 169 &&
             bytes[1] == 254) ||
            (bytes[0] == 172 &&
             bytes[1] is >= 16 and <= 31) ||
            (bytes[0] == 192 &&
             bytes[1] == 0 &&
             bytes[2] == 0) ||
            (bytes[0] == 192 &&
             bytes[1] == 0 &&
             bytes[2] == 2) ||
            (bytes[0] == 192 &&
             bytes[1] == 168) ||
            (bytes[0] == 198 &&
             bytes[1] is 18 or 19) ||
            (bytes[0] == 198 &&
             bytes[1] == 51 &&
             bytes[2] == 100) ||
            (bytes[0] == 203 &&
             bytes[1] == 0 &&
             bytes[2] == 113) ||
            bytes[0] >= 224;
    }

    private static bool IsNonPublicIpv6(
        byte[] bytes)
    {
        bool isUniqueLocal =
            (bytes[0] & 0xFE) == 0xFC;

        bool isLinkLocal =
            bytes[0] == 0xFE &&
            (bytes[1] & 0xC0) == 0x80;

        bool isSiteLocal =
            bytes[0] == 0xFE &&
            (bytes[1] & 0xC0) == 0xC0;

        bool isMulticast =
            bytes[0] == 0xFF;

        bool isDocumentation =
            bytes[0] == 0x20 &&
            bytes[1] == 0x01 &&
            bytes[2] == 0x0D &&
            bytes[3] == 0xB8;

        return isUniqueLocal ||
               isLinkLocal ||
               isSiteLocal ||
               isMulticast ||
               isDocumentation;
    }
}
