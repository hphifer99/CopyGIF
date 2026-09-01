namespace CopyGIF.Core.Policies;

public static class MediaPolicy
{
    public const long MaximumGifBytes =
        50L * 1024L * 1024L;

    public const long MaximumThumbnailBytes =
        5L * 1024L * 1024L;

    public const long MaximumPreviewBytes =
        20L * 1024L * 1024L;

    public const long MaximumThumbnailCacheBytes =
        256L * 1024L * 1024L;

    public const long MaximumPreviewCacheBytes =
        512L * 1024L * 1024L;

    public const long MaximumClipboardCacheBytes =
        1024L * 1024L * 1024L;

    public const int MaximumRedirects = 5;

    public const int MaximumConcurrentMediaRequests = 6;

    public const int MaximumActiveAnimatedPreviews = 12;

    public static TimeSpan PreviewRetention { get; } =
        TimeSpan.FromDays(7);

    public static TimeSpan ClipboardRetention { get; } =
        TimeSpan.FromHours(24);
}
