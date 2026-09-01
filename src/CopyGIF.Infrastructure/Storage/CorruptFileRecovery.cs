using System.Globalization;
using CopyGIF.Core.Policies;

namespace CopyGIF.Infrastructure.Storage;

public sealed class CorruptFileRecovery
{
    private readonly TimeProvider _timeProvider;

    public CorruptFileRecovery()
    {
        _timeProvider = TimeProvider.System;
    }

    public string? Preserve(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            path);

        string fullPath =
            Path.GetFullPath(
                path);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        FileAttributes attributes =
            File.GetAttributes(
                fullPath);

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "A reparse-point file cannot be preserved as corrupt CopyGIF data.");
        }

        string directory =
            Path.GetDirectoryName(
                fullPath) ??
            throw new InvalidOperationException(
                "The corrupt file does not have a parent directory.");

        string fileName =
            Path.GetFileName(
                fullPath);

        string timestamp =
            _timeProvider.GetUtcNow()
                .ToString(
                    "yyyyMMdd'T'HHmmssfffffff'Z'",
                    CultureInfo.InvariantCulture);

        string preservedPath =
            GetAvailablePath(
                directory,
                fileName +
                ".corrupt." +
                timestamp);

        File.Move(
            fullPath,
            preservedPath);

        PruneOldPreservedFiles(
            directory,
            fileName);

        return preservedPath;
    }

    private static string GetAvailablePath(
        string directory,
        string baseFileName)
    {
        string candidate =
            Path.Combine(
                directory,
                baseFileName);

        int suffix = 1;

        while (File.Exists(candidate))
        {
            candidate =
                Path.Combine(
                    directory,
                    baseFileName +
                    "." +
                    suffix.ToString(
                        CultureInfo.InvariantCulture));

            suffix++;
        }

        return candidate;
    }

    private static void PruneOldPreservedFiles(
        string directory,
        string originalFileName)
    {
        string searchPattern =
            originalFileName +
            ".corrupt.*";

        FileInfo[] preservedFiles =
            new DirectoryInfo(
                directory)
                .EnumerateFiles(
                    searchPattern,
                    SearchOption.TopDirectoryOnly)
                .Where(
                    static file =>
                        (file.Attributes &
                         FileAttributes.ReparsePoint) == 0)
                .OrderByDescending(
                    static file => file.Name,
                    StringComparer.Ordinal)
                .ToArray();

        foreach (FileInfo oldFile
                 in preservedFiles.Skip(
                     StoragePolicy
                         .MaximumPreservedCorruptFiles))
        {
            oldFile.Delete();
        }
    }
}
