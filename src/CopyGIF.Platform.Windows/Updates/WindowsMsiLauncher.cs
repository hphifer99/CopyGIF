using System.Diagnostics;
using CopyGIF.Core.Models;

namespace CopyGIF.Platform.Windows.Updates;

internal interface IUpdatePackageLauncher
{
    Task LaunchAsync(
        string packagePath,
        CancellationToken cancellationToken);

    // The default keeps every existing implementer working. The real Windows launcher
    // overrides it to honor the silent and restart options.
    Task LaunchAsync(
        string packagePath,
        UpdateInstallOptions options,
        CancellationToken cancellationToken) =>
        LaunchAsync(
            packagePath,
            cancellationToken);
}

internal sealed class WindowsMsiLauncher :
    IUpdatePackageLauncher
{
    // A public Windows Installer property. The CopyGIF installer package reads it and starts
    // CopyGIF again when the installation ends. No script or helper program is involved.
    internal const string RestartApplicationProperty =
        "RESTARTCOPYGIF=1";

    public Task LaunchAsync(
        string packagePath,
        CancellationToken cancellationToken) =>
        LaunchCoreAsync(
            packagePath,
            UpdateInstallOptions.Interactive,
            cancellationToken);

    public Task LaunchAsync(
        string packagePath,
        UpdateInstallOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            options);

        return LaunchCoreAsync(
            packagePath,
            options,
            cancellationToken);
    }

    private static Task LaunchCoreAsync(
        string packagePath,
        UpdateInstallOptions options,
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

        using Process? process =
            Process.Start(
                CreateStartInfo(
                    fullPath,
                    options));

        if (process is null)
        {
            throw new InvalidOperationException(
                "Windows Installer could not be started.");
        }

        return Task.CompletedTask;
    }

    internal static ProcessStartInfo CreateStartInfo(
        string fullPath,
        UpdateInstallOptions options)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "msiexec.exe"),
            UseShellExecute = true
        };

        // The administrator (UAC) prompt is only for a per-machine installation.
        if (options.RequiresElevation)
        {
            startInfo.Verb = "runas";
        }

        startInfo.ArgumentList.Add("/i");
        startInfo.ArgumentList.Add(fullPath);

        if (options.Silent)
        {
            // No window at all.
            startInfo.ArgumentList.Add("/qn");
            startInfo.ArgumentList.Add("/norestart");
        }
        else if (options.RestartApplication)
        {
            // A small progress bar without any questions, then CopyGIF starts again.
            startInfo.ArgumentList.Add("/passive");
            startInfo.ArgumentList.Add("/norestart");
        }

        // An elevated installer would start CopyGIF with administrator rights, which must
        // not happen, so only a per-user installation asks the package to start CopyGIF.
        if (options.RestartApplication &&
            !options.RequiresElevation)
        {
            startInfo.ArgumentList.Add(
                RestartApplicationProperty);
        }

        return startInfo;
    }
}
