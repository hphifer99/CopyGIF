namespace CopyGIF.Core.Models;

public sealed record GifItem
{
    public required string ProviderId { get; init; }

    public required string Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public required Uri ThumbnailUri { get; init; }

    public required Uri GifUri { get; init; }

    public Uri? PreviewUri { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public string Identity =>
        $"{ProviderId}:{Id}";
}