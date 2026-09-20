using System.ComponentModel;
using System.Diagnostics;

namespace CopyGIF.Platform.Windows.Shell;

/// <summary>
/// Opens one of CopyGIF's own folders in File Explorer. The folder is created first so
/// that the button still works before the app has written anything into it.
/// </summary>
public static class ShellFolderLauncher
{
    public static bool TryOpen(
        string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            folderPath);

        try
        {
            Directory.CreateDirectory(
                folderPath);

            ProcessStartInfo startInfo =
                new()
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true
                };

            startInfo.ArgumentList.Add(
                Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(
                        folderPath)));

            using Process? process =
                Process.Start(startInfo);

            return true;
        }
        catch (Exception exception)
            when (exception is Win32Exception
                or IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException
                or PathTooLongException
                or InvalidOperationException)
        {
            return false;
        }
    }
}
