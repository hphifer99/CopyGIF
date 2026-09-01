namespace CopyGIF.Core.Models;

public enum GifProviderFailure
{
    MissingCredential,
    Unauthorized,
    RateLimited,
    Network,
    Timeout,
    ServiceUnavailable,
    InvalidResponse,
    Unknown
}
