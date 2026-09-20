using System.Runtime.InteropServices;
using CopyGIF.Core.Policies;

namespace CopyGIF.Platform.Windows.Storage;

/// <summary>
/// Resolves the folder CopyGIF stages clipboard GIFs in.
/// A file published through CF_HDROP has to sit at a path the receiving application
/// can open, so this folder is never allowed to be a virtualized AppData path.
/// </summary>
public static class WindowsStagingPaths
{
    private const int Success = 0;
    private const int InsufficientBuffer = 122;
    private const int NoPackageIdentity = 15700;

    private const string PackagesDirectoryName =
        "Packages";

    private const string PackageLocalCacheDirectoryName =
        "LocalCache";

    public static string? PackageFamilyName { get; } =
        ReadPackageFamilyName();

    public static bool IsPackaged =>
        PackageFamilyName is not null;

    public static string ClipboardStagingDirectory { get; } =
        ResolveClipboardStagingDirectory();

    /// <summary>
    /// A single log safe line. It carries folder names only, never credentials.
    /// </summary>
    public static string Describe()
    {
        return IsPackaged
            ? $"packaged family={PackageFamilyName} clipboard={ClipboardStagingDirectory}"
            : $"unpackaged clipboard={ClipboardStagingDirectory}";
    }

    private static string ResolveClipboardStagingDirectory()
    {
        string temporaryStaging =
            Path.Combine(
                Path.GetTempPath(),
                StoragePolicy.LibraryRootDirectoryName,
                StoragePolicy.ClipboardCacheDirectoryName);

        if (PackageFamilyName is not string familyName)
        {
            // An unpackaged build writes to the per user temp folder, the same place
            // the earlier CopyGIF release staged files that other applications could open.
            return temporaryStaging;
        }

        // A packaged build writes inside its own container folder. That folder is a real
        // path on disk, unlike a write through the virtualized AppData namespace.
        string packageStaging =
            Path.Combine(
                ReadPackageLocalCacheDirectory() ??
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder
                            .LocalApplicationData),
                    PackagesDirectoryName,
                    familyName,
                    PackageLocalCacheDirectoryName),
                StoragePolicy.LibraryRootDirectoryName,
                StoragePolicy.ClipboardCacheDirectoryName);

        return CanCreateDirectory(
                packageStaging)
            ? packageStaging
            : temporaryStaging;
    }

    private static string? ReadPackageLocalCacheDirectory()
    {
        try
        {
            // Windows reports the container folder itself, which avoids assuming the
            // layout under the user's Packages folder.
            return global::Windows.Storage.ApplicationData
                .Current
                .LocalCacheFolder
                .Path;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool CanCreateDirectory(
        string path)
    {
        try
        {
            Directory.CreateDirectory(
                path);

            return true;
        }
        catch (Exception exception)
            when (exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            return false;
        }
    }

    private static string? ReadPackageFamilyName()
    {
        uint length = 0;

        int result =
            GetCurrentPackageFamilyName(
                ref length,
                null);

        if (result == NoPackageIdentity ||
            length == 0)
        {
            return null;
        }

        if (result != Success &&
            result != InsufficientBuffer)
        {
            return null;
        }

        char[] buffer =
            new char[length];

        if (GetCurrentPackageFamilyName(
                ref length,
                buffer) != Success ||
            length == 0)
        {
            return null;
        }

        // The returned length counts the terminating null character.
        return new string(
            buffer,
            0,
            checked((int)length) - 1);
    }

    [DllImport(
        "kernel32.dll",
        EntryPoint = "GetCurrentPackageFamilyName",
        CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(
        ref uint packageFamilyNameLength,
        [Out] char[]? packageFamilyName);
}
