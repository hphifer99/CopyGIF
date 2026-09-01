namespace CopyGIF.Core.Models;

public sealed record SearchHistoryEntry
{
    public required string Query { get; init; }

    public required DateTimeOffset LastUsedAtUtc { get; init; }

    public int UseCount { get; init; } = 1;
}

public sealed record SearchHistorySnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public IReadOnlyList<SearchHistoryEntry> Entries { get; init; } =
        [];
}
