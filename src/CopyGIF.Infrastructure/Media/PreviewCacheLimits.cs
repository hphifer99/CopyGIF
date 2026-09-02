using CopyGIF.Core.Policies;

namespace CopyGIF.Infrastructure.Media;

public sealed record PreviewCacheLimits
{
    public static PreviewCacheLimits Default
    {
        get;
    } = new();

    public long MaximumThumbnailBytes
    {
        get;
        init;
    } = MediaPolicy.MaximumThumbnailBytes;

    public long MaximumPreviewBytes
    {
        get;
        init;
    } = MediaPolicy.MaximumPreviewBytes;

    public long MaximumThumbnailCacheBytes
    {
        get;
        init;
    } = MediaPolicy.MaximumThumbnailCacheBytes;

    public long MaximumPreviewCacheBytes
    {
        get;
        init;
    } = MediaPolicy.MaximumPreviewCacheBytes;

    public TimeSpan Retention
    {
        get;
        init;
    } = MediaPolicy.PreviewRetention;

    public void Validate()
    {
        ValidatePositive(
            MaximumThumbnailBytes,
            nameof(MaximumThumbnailBytes));

        ValidatePositive(
            MaximumPreviewBytes,
            nameof(MaximumPreviewBytes));

        ValidatePositive(
            MaximumThumbnailCacheBytes,
            nameof(MaximumThumbnailCacheBytes));

        ValidatePositive(
            MaximumPreviewCacheBytes,
            nameof(MaximumPreviewCacheBytes));

        if (MaximumThumbnailBytes >
            MaximumThumbnailCacheBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumThumbnailBytes),
                MaximumThumbnailBytes,
                "The thumbnail item limit cannot exceed its cache limit.");
        }

        if (MaximumPreviewBytes >
            MaximumPreviewCacheBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumPreviewBytes),
                MaximumPreviewBytes,
                "The preview item limit cannot exceed its cache limit.");
        }

        if (Retention <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Retention),
                Retention,
                "Cache retention must be positive.");
        }
    }

    private static void ValidatePositive(
        long value,
        string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Cache limits must be positive.");
        }
    }
}
