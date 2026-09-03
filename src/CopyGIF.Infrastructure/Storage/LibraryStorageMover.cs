using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Infrastructure.Storage;

public sealed class LibraryStorageMover :
    ILibraryStorageMover
{
    private const int CopyBufferSize =
        128 * 1024;

    private readonly OwnedPathGuard _pathGuard;

    private readonly StringComparer _pathComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    public LibraryStorageMover(
        OwnedPathGuard pathGuard)
    {
        _pathGuard =
            pathGuard ??
            throw new ArgumentNullException(
                nameof(pathGuard));
    }

    public async Task<LibraryStorageMoveResult>
        MoveAsync(
            string sourceOwnedRoot,
            string destinationOwnedRoot,
            IReadOnlyCollection<string> filePaths,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourceOwnedRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            destinationOwnedRoot);

        ArgumentNullException.ThrowIfNull(
            filePaths);

        cancellationToken.ThrowIfCancellationRequested();

        string canonicalSourceRoot =
            CanonicalizeDirectory(
                sourceOwnedRoot);

        string canonicalDestinationRoot =
            CanonicalizeDirectory(
                destinationOwnedRoot);

        _pathGuard.EnsureSafeDirectory(
            canonicalDestinationRoot,
            canonicalDestinationRoot);

        List<MovePlan> plans =
            CreateMovePlans(
                canonicalSourceRoot,
                canonicalDestinationRoot,
                filePaths);

        Dictionary<string, string> movedPaths =
            new(_pathComparer);

        if (_pathComparer.Equals(
                canonicalSourceRoot,
                canonicalDestinationRoot))
        {
            foreach (MovePlan plan in plans)
            {
                movedPaths[plan.SourcePath] =
                    plan.SourcePath;
            }

            return new LibraryStorageMoveResult
            {
                MovedPaths = movedPaths
            };
        }

        List<string> createdDestinationFiles = [];
        List<string> temporaryFiles = [];

        try
        {
            foreach (MovePlan plan in plans)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                string destinationDirectory =
                    Path.GetDirectoryName(
                        plan.DestinationPath) ??
                    throw new InvalidDataException(
                        "A library destination does not have a parent directory.");

                _pathGuard.EnsureSafeDirectory(
                    canonicalDestinationRoot,
                    destinationDirectory);

                if (File.Exists(
                        plan.DestinationPath))
                {
                    throw new IOException(
                        $"The library destination file already exists: '{plan.DestinationPath}'.");
                }

                string temporaryPath =
                    _pathGuard.EnsureSafeFilePath(
                        canonicalDestinationRoot,
                        Path.Combine(
                            destinationDirectory,
                            $".{Path.GetFileName(plan.DestinationPath)}.{Guid.NewGuid():N}.tmp"));

                temporaryFiles.Add(
                    temporaryPath);

                await CopyFileAsync(
                        plan.SourcePath,
                        temporaryPath,
                        cancellationToken)
                    .ConfigureAwait(false);

                File.Move(
                    temporaryPath,
                    plan.DestinationPath);

                temporaryFiles.Remove(
                    temporaryPath);

                createdDestinationFiles.Add(
                    plan.DestinationPath);

                movedPaths[plan.SourcePath] =
                    plan.DestinationPath;
            }
        }
        catch
        {
            DeleteFilesForRollback(
                canonicalDestinationRoot,
                temporaryFiles);

            DeleteFilesForRollback(
                canonicalDestinationRoot,
                createdDestinationFiles);

            throw;
        }

        List<string> sourceFilesNotDeleted = [];

        foreach (MovePlan plan in plans)
        {
            try
            {
                _pathGuard.DeleteOwnedFileIfPresent(
                    canonicalSourceRoot,
                    plan.SourcePath);
            }
            catch (Exception exception)
                when (exception is IOException or
                      UnauthorizedAccessException or
                      MediaDownloadException)
            {
                sourceFilesNotDeleted.Add(
                    plan.SourcePath);
            }
        }

        return new LibraryStorageMoveResult
        {
            MovedPaths = movedPaths,
            SourceFilesNotDeleted =
                sourceFilesNotDeleted
        };
    }

    public Task DeleteAsync(
        string ownedRoot,
        IReadOnlyCollection<string> filePaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            ownedRoot);

        ArgumentNullException.ThrowIfNull(
            filePaths);

        string canonicalRoot =
            CanonicalizeDirectory(
                ownedRoot);

        IReadOnlyList<string> canonicalFiles =
            NormalizeFilePaths(
                    filePaths)
                .Select(
                    filePath =>
                        _pathGuard.EnsureSafeFilePath(
                            canonicalRoot,
                            filePath))
                .ToArray();

        foreach (string filePath
                 in canonicalFiles)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            _pathGuard.DeleteOwnedFileIfPresent(
                canonicalRoot,
                filePath);
        }

        return Task.CompletedTask;
    }

    private List<MovePlan>
        CreateMovePlans(
            string sourceOwnedRoot,
            string destinationOwnedRoot,
            IReadOnlyCollection<string> filePaths)
    {
        List<MovePlan> plans = [];

        foreach (string filePath
                 in NormalizeFilePaths(
                     filePaths))
        {
            string canonicalSource =
                _pathGuard.EnsureSafeFilePath(
                    sourceOwnedRoot,
                    filePath);

            if (!File.Exists(
                    canonicalSource))
            {
                continue;
            }

            string relativePath =
                Path.GetRelativePath(
                    sourceOwnedRoot,
                    canonicalSource);

            string canonicalDestination =
                _pathGuard.EnsureSafeFilePath(
                    destinationOwnedRoot,
                    Path.Combine(
                        destinationOwnedRoot,
                        relativePath));

            plans.Add(
                new MovePlan(
                    canonicalSource,
                    canonicalDestination));
        }

        return plans;
    }

    private IReadOnlyList<string>
        NormalizeFilePaths(
            IEnumerable<string> filePaths)
    {
        HashSet<string> uniquePaths =
            new(_pathComparer);

        foreach (string? filePath in filePaths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                filePath);

            uniquePaths.Add(
                Path.GetFullPath(
                    filePath));
        }

        return [.. uniquePaths];
    }

    private static async Task CopyFileAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using FileStream source =
            new(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferSize,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        await using FileStream destination =
            new(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                CopyBufferSize,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        await source.CopyToAsync(
                destination,
                CopyBufferSize,
                cancellationToken)
            .ConfigureAwait(false);

        await destination.FlushAsync(
                cancellationToken)
            .ConfigureAwait(false);

        if (destination.Length != source.Length)
        {
            throw new IOException(
                "A library file copy did not preserve the complete file length.");
        }
    }

    private void DeleteFilesForRollback(
        string ownedRoot,
        IEnumerable<string> filePaths)
    {
        foreach (string filePath in filePaths)
        {
            try
            {
                _pathGuard.DeleteOwnedFileIfPresent(
                    ownedRoot,
                    filePath);
            }
            catch (Exception exception)
                when (exception is IOException or
                      UnauthorizedAccessException or
                      MediaDownloadException)
            {
            }
        }
    }

    private static string CanonicalizeDirectory(
        string path)
    {
        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(
                path));
    }

    private sealed record MovePlan(
        string SourcePath,
        string DestinationPath);
}
