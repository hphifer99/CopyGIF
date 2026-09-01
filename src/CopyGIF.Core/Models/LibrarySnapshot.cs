namespace CopyGIF.Core.Models;

public sealed record LibrarySnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public IReadOnlyList<LibraryEntry> Favorites { get; init; } =
        [];

    public IReadOnlyList<LibraryEntry> Recents { get; init; } =
        [];
}
