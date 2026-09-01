namespace CopyGIF.Core.Models;

public enum MigrationStatus
{
    None,
    NotRequired,
    Completed,
    Failed,
    RolledBack
}

public sealed record MigrationResult
{
    public required MigrationStatus Status { get; init; }

    public int MigratedFavorites { get; init; }

    public int MigratedRecents { get; init; }

    public bool MigratedSettings { get; init; }

    public bool MigratedCredential { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } =
        [];

    public string? Message { get; init; }

    public bool Succeeded =>
        Status is
            MigrationStatus.NotRequired or
            MigrationStatus.Completed;
}

public sealed record MigrationState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } =
        CurrentSchemaVersion;

    public bool IsCompleted { get; init; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public string? SourceVersion { get; init; }
}
