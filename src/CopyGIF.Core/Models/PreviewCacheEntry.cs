namespace CopyGIF.Core.Models;

public enum PreviewCacheKind
{
    Thumbnail,
    Preview
}

public sealed record PreviewCacheEntry
{
    public required Uri SourceUri { get; init; }

    public required PreviewCacheKind Kind { get; init; }

    public required string FilePath { get; init; }

    public required long SizeBytes { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }

    public required DateTimeOffset LastAccessedAtUtc { get; init; }
}
