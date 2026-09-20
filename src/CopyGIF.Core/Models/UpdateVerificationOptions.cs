namespace CopyGIF.Core.Models;

/// <summary>
/// How a downloaded update package is verified. The default is the full check.
/// </summary>
public sealed record UpdateVerificationOptions
{
    public static UpdateVerificationOptions Full { get; } =
        new();

    /// <summary>
    /// Ask the certificate authority's revocation servers whether the signing certificate is
    /// still valid. Turn this off only for a package that was already fully verified when it
    /// was downloaded and must be checked again while the network is not worth waiting for
    /// (for example at application start). The signature, the certificate chain, the
    /// publisher, the size and the SHA-256 hash are still checked.
    /// </summary>
    public bool CheckRevocationOnline { get; init; } = true;
}
