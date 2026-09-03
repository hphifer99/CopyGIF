namespace CopyGIF.Core.Contracts;

public interface ILibraryStorageMover
{
    Task<LibraryStorageMoveResult> MoveAsync(
        string sourceOwnedRoot,
        string destinationOwnedRoot,
        IReadOnlyCollection<string> filePaths,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string ownedRoot,
        IReadOnlyCollection<string> filePaths,
        CancellationToken cancellationToken = default);
}

public sealed record LibraryStorageMoveResult
{
    public IReadOnlyDictionary<string, string> MovedPaths { get; init; } =
        new Dictionary<string, string>();

    public IReadOnlyList<string> SourceFilesNotDeleted { get; init; } =
        [];
}
