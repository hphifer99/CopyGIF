namespace CopyGIF.Core.Models;

public sealed record LibraryEntry
{
    public required GifIdentity Identity { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public required Uri GifUri { get; init; }

    public required Uri ThumbnailUri { get; init; }

    public Uri? PreviewUri { get; init; }

    public Uri? SourcePageUri { get; init; }

    public string? LocalFilePath { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public long? SizeBytes { get; init; }

    public required DateTimeOffset AddedAtUtc { get; init; }

    public DateTimeOffset? LastCopiedAtUtc { get; init; }

    public int CopyCount { get; init; }
}
