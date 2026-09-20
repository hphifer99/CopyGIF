namespace CopyGIF.Core.Models;

[Flags]
public enum ProviderCapabilities
{
    None = 0,

    Search = 1 << 0,

    Trending = 1 << 1,

    Pagination = 1 << 2,

    CredentialValidation = 1 << 3,

    ShareRegistration = 1 << 4
}
