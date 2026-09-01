namespace CopyGIF.Core.Models;

public sealed record GifSearchPage
{
    public required IReadOnlyList<GifItem> Items { get; init; }

    public string? ContinuationToken { get; init; }

    public int? TotalCount { get; init; }

    public bool HasMore =>
        !string.IsNullOrWhiteSpace(
            ContinuationToken);

    public static GifSearchPage Empty()
    {
        return new GifSearchPage
        {
            Items = []
        };
    }
}
