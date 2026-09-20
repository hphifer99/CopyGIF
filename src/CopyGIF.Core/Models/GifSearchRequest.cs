using CopyGIF.Core.Settings;

namespace CopyGIF.Core.Models;

public enum GifSearchKind
{
    Search,
    Trending
}

public sealed record GifSearchRequest
{
    public GifContentRating ContentRating { get; init; } =
        GifContentRating.Pg;

    public required string Query { get; init; }

    public GifSearchKind Kind { get; init; } =
        GifSearchKind.Search;

    public int PageSize { get; init; } = 24;

    public string? ContinuationToken { get; init; }
}
