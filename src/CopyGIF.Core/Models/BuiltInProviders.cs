namespace CopyGIF.Core.Models;

/// <summary>
/// The descriptors of the providers that ship with CopyGIF. They live in Core so that the
/// provider rules (for example the direct media hosts) have defaults before any service
/// container exists. The Infrastructure layer registers these same instances.
/// </summary>
public static class BuiltInProviders
{
    public const string KlipyId = "klipy";

    public const string GiphyId = "giphy";

    public static ProviderDescriptor Klipy { get; } =
        new()
        {
            Id = KlipyId,
            DisplayName = "KLIPY",
            Capabilities =
                ProviderCapabilities.Search |
                ProviderCapabilities.Trending |
                ProviderCapabilities.Pagination |
                ProviderCapabilities.CredentialValidation |
                ProviderCapabilities.ShareRegistration,
            RequiresCredential = true,
            AttributionText = "Powered by KLIPY",
            AttributionUri = new Uri("https://klipy.com/"),
            CredentialHelpUri = new Uri("https://partner.klipy.com/api-keys"),
            // Documented by KLIPY under "Network requirements" (docs.klipy.com/network-requirements)
            // as the hosts that deliver GIFs, stickers and thumbnails.
            MediaHosts = ["static.klipy.com", "static1.klipy.com", "static2.klipy.com"],
            UsesDirectPreview = false,
            AllowsPersistentLibrary = true,
            CachesTrendingSnapshot = true,
            DeduplicatesResults = true
        };

    public static ProviderDescriptor Giphy { get; } =
        new()
        {
            Id = GiphyId,
            DisplayName = "GIPHY",
            Capabilities =
                ProviderCapabilities.Search |
                ProviderCapabilities.Trending |
                ProviderCapabilities.Pagination |
                ProviderCapabilities.CredentialValidation,
            RequiresCredential = true,
            AttributionText = "Powered By GIPHY",
            AttributionUri = new Uri("https://giphy.com/"),
            AttributionImageAsset = "Assets/PoweredByGiphy.png",
            CredentialHelpUri = new Uri("https://developers.giphy.com/dashboard/"),
            CredentialInstructions = "For GIPHY, choose API instead of SDK.",
            // GIPHY does not publish a host list. Its documentation shows media.giphy.com, media0,
            // media1 and media4 in media addresses. media2 and media3 follow the same numbering and
            // appear in public traffic listings. A GIF on any other GIPHY host is skipped, and the
            // host name is written to the repair log (stage media-host-rejected).
            MediaHosts =
            [
                "media.giphy.com", "media0.giphy.com", "media1.giphy.com",
                "media2.giphy.com", "media3.giphy.com", "media4.giphy.com"
            ],
            UsesDirectPreview = true,
            AllowsPersistentLibrary = true,
            CachesTrendingSnapshot = false,
            DeduplicatesResults = false
        };

    public static IReadOnlyList<ProviderDescriptor> All { get; } =
        [Klipy, Giphy];
}
