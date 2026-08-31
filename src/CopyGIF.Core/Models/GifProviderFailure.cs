namespace CopyGIF.Core.Models;

public enum GifProviderFailure
{
    MissingCredential,
    Unauthorized,
    RateLimited,
    Network,
    ServiceUnavailable,
    InvalidResponse,
    Unknown
}