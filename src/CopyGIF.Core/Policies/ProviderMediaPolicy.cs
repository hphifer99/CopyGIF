namespace CopyGIF.Core.Policies;

public static class ProviderMediaPolicy
{
    public static bool AllowsPersistentLibrary(string providerId) =>
        string.Equals(providerId, "klipy", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(providerId, "giphy", StringComparison.OrdinalIgnoreCase);

    public static bool UsesDirectPreview(string providerId) =>
        string.Equals(providerId, "giphy", StringComparison.OrdinalIgnoreCase);

    public static bool IsDirectMediaUri(Uri? uri) => uri is { IsAbsoluteUri: true } &&
        uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) &&
        (uri.IdnHost.Equals("media.giphy.com", StringComparison.OrdinalIgnoreCase) ||
         uri.IdnHost.Equals("media0.giphy.com", StringComparison.OrdinalIgnoreCase) ||
         uri.IdnHost.Equals("media1.giphy.com", StringComparison.OrdinalIgnoreCase) ||
         uri.IdnHost.Equals("media2.giphy.com", StringComparison.OrdinalIgnoreCase) ||
         uri.IdnHost.Equals("media3.giphy.com", StringComparison.OrdinalIgnoreCase) ||
         uri.IdnHost.Equals("media4.giphy.com", StringComparison.OrdinalIgnoreCase));
}
