namespace CopyGIF.Core.Models;

public enum GifDownloadPurpose
{
    Clipboard,
    Favorite,
    Recent
}

public sealed record DownloadedGif
{
    public required GifIdentity Identity { get; init; }

    public required Uri SourceUri { get; init; }

    public required string FilePath { get; init; }

    public required long SizeBytes { get; init; }

    public required string Sha256 { get; init; }

    public required DateTimeOffset DownloadedAtUtc { get; init; }

    public required GifDownloadPurpose Purpose { get; init; }
}
