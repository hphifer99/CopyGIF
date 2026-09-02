using System.Diagnostics;

namespace CopyGIF.Platform.Windows.Updates;

internal interface IUpdatePackageLauncher
{
    Task LaunchAsync(
        string packagePath,
        CancellationToken cancellationToken);
}

internal sealed class WindowsMsiLauncher :
    IUpdatePackageLauncher
{
    public Task LaunchAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            packagePath);

        cancellationToken.ThrowIfCancellationRequested();

        string fullPath =
            Path.GetFullPath(
                packagePath);

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".msi",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only verified Windows Installer packages can be launched.");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "The verified update package no longer exists.",
                fullPath);
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = "msiexec.exe",
            UseShellExecute = true,
            Verb = "runas"
        };

        startInfo.ArgumentList.Add("/i");
        startInfo.ArgumentList.Add(fullPath);

        using Process? process =
            Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException(
                "Windows Installer could not be started.");
        }

        return Task.CompletedTask;
    }
}
