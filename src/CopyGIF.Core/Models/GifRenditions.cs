using CopyGIF.Core.Settings;

namespace CopyGIF.Core.Models;

public sealed record GifRenditions
{
    public Uri? Minimum { get; init; }
    public Uri? Low { get; init; }
    public Uri? Medium { get; init; }
    public Uri? High { get; init; }
    public Uri? Maximum { get; init; }

    // A provider can supply fewer than five distinct GIF sizes. The original
    // animated GIF remains the safe fallback, never a still or a video.
    public Uri Select(GifQuality quality, Uri original) => quality switch
    {
        GifQuality.Minimum => Minimum ?? Low ?? Medium ?? High ?? Maximum ?? original,
        GifQuality.Low => Low ?? Medium ?? Minimum ?? High ?? Maximum ?? original,
        GifQuality.Medium => Medium ?? Low ?? High ?? Minimum ?? Maximum ?? original,
        GifQuality.High => High ?? Medium ?? Maximum ?? Low ?? Minimum ?? original,
        GifQuality.Maximum => Maximum ?? original,
        _ => original
    };
}
