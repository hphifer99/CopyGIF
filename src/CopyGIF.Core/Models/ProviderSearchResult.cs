namespace CopyGIF.Core.Models;

public sealed record ProviderSearchResult
{
    public required ProviderDescriptor Provider { get; init; }

    public required GifSearchPage Page { get; init; }

    public IReadOnlyList<GifItem> Items =>
        Page.Items;

    public bool HasMore =>
        Page.HasMore;
}
