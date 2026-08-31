namespace CopyGIF.Core.Models;

public sealed record GifSearchRequest
{
    public required string Query { get; init; }

    public int PageSize { get; init; } = 24;

    public string? ContinuationToken { get; init; }
}