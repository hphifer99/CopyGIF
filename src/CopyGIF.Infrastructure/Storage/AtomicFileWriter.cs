using System.Collections.Concurrent;

namespace CopyGIF.Infrastructure.Storage;

public sealed class AtomicFileWriter
{
    private readonly ConcurrentDictionary<
        string,
        SemaphoreSlim> _pathLocks =
            new(
                StringComparer.OrdinalIgnoreCase);

    public async Task WriteAsync(
        string destinationPath,
        string backupPath,
        Func<Stream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            destinationPath);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            backupPath);

        ArgumentNullException.ThrowIfNull(
            writeAsync);

        string fullDestinationPath =
            Path.GetFullPath(
                destinationPath);

        string fullBackupPath =
            Path.GetFullPath(
                backupPath);

        if (string.Equals(
                fullDestinationPath,
                fullBackupPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The destination and backup paths must be different.",
                nameof(backupPath));
        }

        string? destinationDirectory =
            Path.GetDirectoryName(
                fullDestinationPath);

        string? backupDirectory =
            Path.GetDirectoryName(
                fullBackupPath);

        if (string.IsNullOrWhiteSpace(
                destinationDirectory) ||
            !string.Equals(
                destinationDirectory,
                backupDirectory,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The destination and backup must use the same directory.",
                nameof(backupPath));
        }

        Directory.CreateDirectory(
            destinationDirectory);

        SemaphoreSlim pathLock =
            _pathLocks.GetOrAdd(
                fullDestinationPath,
                static _ => new SemaphoreSlim(1, 1));

        await pathLock.WaitAsync(
            cancellationToken);

        string temporaryPath =
            fullDestinationPath +
            "." +
            Guid.NewGuid().ToString("N") +
            ".tmp";

        try
        {
            await using (
                FileStream stream =
                    new(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 16 * 1024,
                        options:
                            FileOptions.Asynchronous |
                            FileOptions.WriteThrough))
            {
                await writeAsync(
                    stream,
                    cancellationToken);

                await stream.FlushAsync(
                    cancellationToken);

                stream.Flush(
                    flushToDisk: true);
            }

            cancellationToken
                .ThrowIfCancellationRequested();

            if (File.Exists(
                    fullDestinationPath))
            {
                File.Replace(
                    temporaryPath,
                    fullDestinationPath,
                    fullBackupPath,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(
                    temporaryPath,
                    fullDestinationPath);
            }
        }
        finally
        {
            TryDelete(
                temporaryPath);

            pathLock.Release();
        }
    }

    private static void TryDelete(
        string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
