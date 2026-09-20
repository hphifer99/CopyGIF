using CopyGIF.Core.Models;

namespace CopyGIF.Core.Policies;

/// <summary>
/// Answers provider specific media questions from the registered provider descriptors.
/// The built-in providers are known from the start. The application calls
/// <see cref="Configure"/> once at start-up with every registered descriptor, so a provider that is
/// added later needs no change here.
/// </summary>
public static class ProviderMediaPolicy
{
    private sealed class Rules
    {
        public required IReadOnlyDictionary<string, ProviderDescriptor> ById { get; init; }

        public required HashSet<string> DirectHosts { get; init; }
    }

    private static volatile Rules _rules = Build(BuiltInProviders.All);

    /// <summary>Replaces the rules with the ones from these descriptors.</summary>
    public static void Configure(IEnumerable<ProviderDescriptor> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        Rules rules = Build(providers);

        if (rules.ById.Count == 0)
        {
            throw new ArgumentException(
                "At least one provider descriptor is required.",
                nameof(providers));
        }

        _rules = rules;
    }

    public static bool AllowsPersistentLibrary(string providerId) =>
        TryGet(providerId)?.AllowsPersistentLibrary == true;

    public static bool UsesDirectPreview(string providerId) =>
        TryGet(providerId)?.UsesDirectPreview == true;

    /// <summary>
    /// The name to show for a provider id. An unknown id is shown as it was given, so a card that
    /// was saved by a provider that is no longer registered still has a label.
    /// </summary>
    public static string DisplayNameFor(string? providerId)
    {
        string trimmed = providerId?.Trim() ?? string.Empty;

        return TryGet(trimmed)?.DisplayName ?? trimmed;
    }

    /// <summary>
    /// True when the address is a plain HTTPS address on a media host of a provider that loads its
    /// previews directly.
    /// </summary>
    public static bool IsDirectMediaUri(Uri? uri) =>
        uri is { IsAbsoluteUri: true } &&
        uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) &&
        _rules.DirectHosts.Contains(uri.IdnHost);

    /// <summary>
    /// Leaves a trace in the repair log that a media address was refused because its host is not
    /// approved. Without this, a provider that starts serving media from a new host would just
    /// make results disappear. Only the host name is written, never the rest of the address.
    /// </summary>
    public static void RecordRejectedHost(string providerId, Uri? uri)
    {
        string host = uri is { IsAbsoluteUri: true } ? uri.IdnHost : "unparsable";

        // A host name is at most 253 characters. Anything longer is not a host name.
        if (host.Length > 100)
        {
            host = host[..100];
        }

        RepairDiagnostics.Record("media-host-rejected", providerId, $"unlisted:{host}");
    }

    private static ProviderDescriptor? TryGet(string? providerId) =>
        !string.IsNullOrWhiteSpace(providerId) &&
        _rules.ById.TryGetValue(providerId.Trim(), out ProviderDescriptor? descriptor)
            ? descriptor
            : null;

    private static Rules Build(IEnumerable<ProviderDescriptor> providers)
    {
        Dictionary<string, ProviderDescriptor> byId = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> directHosts = new(StringComparer.OrdinalIgnoreCase);

        foreach (ProviderDescriptor descriptor in providers)
        {
            byId[descriptor.Id.Trim()] = descriptor;

            if (descriptor.UsesDirectPreview)
            {
                foreach (string host in descriptor.MediaHosts)
                {
                    directHosts.Add(host.Trim().TrimEnd('.'));
                }
            }
        }

        return new Rules { ById = byId, DirectHosts = directHosts };
    }
}
