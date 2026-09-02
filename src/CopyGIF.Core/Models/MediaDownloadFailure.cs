namespace CopyGIF.Core.Models;

public enum MediaDownloadFailure
{
    InvalidUri,
    UnapprovedHost,
    PrivateNetworkTarget,
    HostResolutionFailed,
    RedirectLimitExceeded,
    Network,
    Timeout,
    HttpError,
    TooLarge,
    InvalidGif,
    UnsafePath,
    Storage
}
