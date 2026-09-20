namespace CopyGIF.Core.Models;

/// <summary>
/// Everything CopyGIF needs to know about one GIF provider, other than how to talk to it.
/// A provider is added by registering one descriptor, one <c>IGifProvider</c> and one
/// <c>IGifProviderCredentialManager</c>. The onboarding window, the API key section of Settings,
/// the search attribution, the media host allowlist and the library rules are all driven by these
/// values, so no other file needs to know the provider's name.
/// </summary>
public sealed record ProviderDescriptor
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public ProviderCapabilities Capabilities { get; init; }

    public bool RequiresCredential { get; init; } = true;

    public string? AttributionText { get; init; }

    public Uri? AttributionUri { get; init; }

    /// <summary>
    /// Optional image shown as the attribution instead of <see cref="AttributionText"/>. This is a
    /// path relative to the app package, for example <c>Assets/PoweredByGiphy.png</c>.
    /// </summary>
    public string? AttributionImageAsset { get; init; }

    /// <summary>The page where a person creates an API key. Shown in onboarding and in Settings.</summary>
    public Uri? CredentialHelpUri { get; init; }

    /// <summary>A short hint shown with the key field, for example which kind of key to create.</summary>
    public string? CredentialInstructions { get; init; }

    /// <summary>
    /// The host names that serve this provider's media. Media is only downloaded over HTTPS from
    /// these hosts. A provider with no hosts listed cannot download any media.
    /// </summary>
    public IReadOnlyList<string> MediaHosts { get; init; } = [];

    /// <summary>
    /// True when the picker may load this provider's previews straight from its media hosts
    /// instead of going through the local preview cache.
    /// </summary>
    public bool UsesDirectPreview { get; init; }

    /// <summary>
    /// True when this provider's GIFs may be kept in Favorites and Recents. This is a decision
    /// about the provider's terms, so it defaults to false and each provider must opt in.
    /// </summary>
    public bool AllowsPersistentLibrary { get; init; }

    /// <summary>True when the first page of trending results is kept in memory and reused.</summary>
    public bool CachesTrendingSnapshot { get; init; }

    /// <summary>True when a result that was already shown in the current list is left out.</summary>
    public bool DeduplicatesResults { get; init; } = true;

    public bool Supports(
        ProviderCapabilities capability)
    {
        return (
            Capabilities &
            capability) == capability;
    }
}
