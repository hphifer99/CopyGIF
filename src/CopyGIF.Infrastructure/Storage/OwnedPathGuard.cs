using CopyGIF.Core.Models;

namespace CopyGIF.Infrastructure.Storage;

public sealed class OwnedPathGuard
{
    private readonly StringComparison
        _pathComparison =
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

    public void EnsureSafeDirectory(
        string ownedRoot,
        string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            ownedRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            destinationDirectory);

        string canonicalRoot =
            CanonicalizeDirectory(
                ownedRoot);

        string canonicalDestination =
            CanonicalizeDirectory(
                destinationDirectory);

        if (!IsWithin(
                canonicalRoot,
                canonicalDestination))
        {
            throw UnsafePath(
                "The media destination is outside its CopyGIF-owned root.");
        }

        CreateAndValidateDirectoryTree(
            canonicalRoot,
            canonicalDestination);
    }

    public string EnsureSafeFilePath(
        string ownedRoot,
        string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            ownedRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        string canonicalRoot =
            CanonicalizeDirectory(
                ownedRoot);

        string canonicalFile =
            Path.GetFullPath(
                filePath);

        if (!IsWithin(
                canonicalRoot,
                canonicalFile))
        {
            throw UnsafePath(
                "The media file is outside its CopyGIF-owned root.");
        }

        string? parentDirectory =
            Path.GetDirectoryName(
                canonicalFile);

        if (parentDirectory is null)
        {
            throw UnsafePath(
                "The media file does not have a safe parent directory.");
        }

        if (Directory.Exists(
                canonicalRoot) &&
            Directory.Exists(
                parentDirectory))
        {
            ValidateDirectoryTree(
                canonicalRoot,
                parentDirectory);
        }

        if (File.Exists(
                canonicalFile))
        {
            FileAttributes attributes =
                File.GetAttributes(
                    canonicalFile);

            if ((attributes &
                 FileAttributes.ReparsePoint) != 0)
            {
                throw UnsafePath(
                    "A media file cannot be a reparse point.");
            }
        }

        return canonicalFile;
    }

    public void DeleteOwnedFileIfPresent(
        string ownedRoot,
        string filePath)
    {
        string canonicalFile =
            EnsureSafeFilePath(
                ownedRoot,
                filePath);

        if (File.Exists(
                canonicalFile))
        {
            File.Delete(
                canonicalFile);
        }
    }

    private static void CreateAndValidateDirectoryTree(
        string canonicalRoot,
        string canonicalDestination)
    {
        Directory.CreateDirectory(
            canonicalRoot);

        ValidateDirectory(
            canonicalRoot);

        string relative =
            Path.GetRelativePath(
                canonicalRoot,
                canonicalDestination);

        if (relative == ".")
        {
            return;
        }

        string current =
            canonicalRoot;

        foreach (string component
                 in SplitRelativePath(
                     relative))
        {
            current =
                Path.Combine(
                    current,
                    component);

            if (Directory.Exists(
                    current))
            {
                ValidateDirectory(
                    current);

                continue;
            }

            Directory.CreateDirectory(
                current);

            ValidateDirectory(
                current);
        }
    }

    private static void ValidateDirectoryTree(
        string canonicalRoot,
        string canonicalDestination)
    {
        ValidateDirectory(
            canonicalRoot);

        string relative =
            Path.GetRelativePath(
                canonicalRoot,
                canonicalDestination);

        if (relative == ".")
        {
            return;
        }

        string current =
            canonicalRoot;

        foreach (string component
                 in SplitRelativePath(
                     relative))
        {
            current =
                Path.Combine(
                    current,
                    component);

            ValidateDirectory(
                current);
        }
    }

    private static string[] SplitRelativePath(
        string relativePath)
    {
        return relativePath.Split(
            [
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            ],
            StringSplitOptions.RemoveEmptyEntries);
    }

    private static void ValidateDirectory(
        string path)
    {
        FileAttributes attributes =
            File.GetAttributes(
                path);

        if ((attributes &
             FileAttributes.Directory) == 0 ||
            (attributes &
             FileAttributes.ReparsePoint) != 0)
        {
            throw UnsafePath(
                "A CopyGIF-owned media directory is unsafe.");
        }
    }

    private static string CanonicalizeDirectory(
        string path)
    {
        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(
                path));
    }

    private bool IsWithin(
        string canonicalRoot,
        string candidate)
    {
        if (string.Equals(
                canonicalRoot,
                candidate,
                _pathComparison))
        {
            return true;
        }

        string prefix =
            canonicalRoot +
            Path.DirectorySeparatorChar;

        return candidate.StartsWith(
            prefix,
            _pathComparison);
    }

    private static MediaDownloadException UnsafePath(
        string message)
    {
        return new MediaDownloadException(
            MediaDownloadFailure.UnsafePath,
            message);
    }
}
