using CopyGIF.Core.Models;
using CopyGIF.Platform.Windows.Updates;

namespace CopyGIF.Platform.Windows.Tests.Updates;

[TestClass]
public sealed class WindowsMsiLauncherTests
{
    [TestMethod]
    public async Task LaunchAsync_NonMsiPackage_Throws()
    {
        WindowsMsiLauncher launcher = new();

        await Assert.ThrowsExactlyAsync<
            InvalidOperationException>(
                () => launcher.LaunchAsync(
                    "CopyGIF.exe",
                    CancellationToken.None));
    }

    [TestMethod]
    public async Task LaunchAsync_MissingMsiPackage_Throws()
    {
        WindowsMsiLauncher launcher = new();

        string packagePath = Path.Combine(
            Path.GetTempPath(),
            $"CopyGIF-{Guid.NewGuid():N}.msi");

        await Assert.ThrowsExactlyAsync<
            FileNotFoundException>(
                () => launcher.LaunchAsync(
                    packagePath,
                    CancellationToken.None));
    }

    private const string PackagePath =
        @"C:\Updates\CopyGIF-2.1.0-win-x64.msi";

    private static List<string> Arguments(
        UpdateInstallOptions options) =>
        WindowsMsiLauncher
            .CreateStartInfo(
                PackagePath,
                options)
            .ArgumentList
            .ToList();

    [TestMethod]
    public void CreateStartInfo_PerMachine_AsksForAdministratorRights()
    {
        System.Diagnostics.ProcessStartInfo startInfo =
            WindowsMsiLauncher.CreateStartInfo(
                PackagePath,
                new UpdateInstallOptions());

        Assert.AreEqual(
            "runas",
            startInfo.Verb);
    }

    [TestMethod]
    public void CreateStartInfo_PerUser_DoesNotAskForAdministratorRights()
    {
        System.Diagnostics.ProcessStartInfo startInfo =
            WindowsMsiLauncher.CreateStartInfo(
                PackagePath,
                new UpdateInstallOptions
                {
                    RequiresElevation = false,
                    Silent = true
                });

        Assert.AreNotEqual(
            "runas",
            startInfo.Verb);

        CollectionAssert.Contains(
            startInfo.ArgumentList.ToList(),
            "/qn");
    }

    [TestMethod]
    public void CreateStartInfo_UsesMsiexecAndTheUninterpretedPackagePath()
    {
        System.Diagnostics.ProcessStartInfo startInfo =
            WindowsMsiLauncher.CreateStartInfo(
                PackagePath,
                new UpdateInstallOptions());

        Assert.AreEqual(
            "msiexec.exe",
            Path.GetFileName(
                startInfo.FileName));

        Assert.AreEqual(
            "/i",
            startInfo.ArgumentList[0]);

        Assert.AreEqual(
            PackagePath,
            startInfo.ArgumentList[1]);
    }

    [TestMethod]
    public void CreateStartInfo_DefaultOptions_KeepTheInstallerWizardAndDoNotRestart()
    {
        List<string> arguments =
            Arguments(
                UpdateInstallOptions.Interactive);

        CollectionAssert.AreEqual(
            new[]
            {
                "/i",
                PackagePath
            },
            arguments);
    }

    [TestMethod]
    public void CreateStartInfo_SilentPerUserRestart_IsQuietAndAsksThePackageToRestartCopyGif()
    {
        List<string> arguments =
            Arguments(
                new UpdateInstallOptions
                {
                    Silent = true,
                    RestartApplication = true,
                    RequiresElevation = false
                });

        CollectionAssert.AreEqual(
            new[]
            {
                "/i",
                PackagePath,
                "/qn",
                "/norestart",
                "RESTARTCOPYGIF=1"
            },
            arguments);
    }

    [TestMethod]
    public void CreateStartInfo_VisiblePerUserRestart_ShowsOnlyAProgressBar()
    {
        List<string> arguments =
            Arguments(
                new UpdateInstallOptions
                {
                    RestartApplication = true,
                    RequiresElevation = false
                });

        CollectionAssert.AreEqual(
            new[]
            {
                "/i",
                PackagePath,
                "/passive",
                "/norestart",
                "RESTARTCOPYGIF=1"
            },
            arguments);
    }

    [TestMethod]
    public void CreateStartInfo_ElevatedRestart_DoesNotAskTheElevatedInstallerToStartCopyGif()
    {
        // An elevated installer would start CopyGIF with administrator rights.
        System.Diagnostics.ProcessStartInfo startInfo =
            WindowsMsiLauncher.CreateStartInfo(
                PackagePath,
                new UpdateInstallOptions
                {
                    RestartApplication = true,
                    RequiresElevation = true
                });

        Assert.AreEqual(
            "runas",
            startInfo.Verb);

        Assert.IsFalse(
            startInfo.ArgumentList.Any(
                argument =>
                    argument.StartsWith(
                        "RESTARTCOPYGIF",
                        StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CreateStartInfo_NoRestartRequested_NeverAddsTheRestartProperty()
    {
        Assert.IsFalse(
            Arguments(
                new UpdateInstallOptions
                {
                    Silent = true,
                    RequiresElevation = false
                }).Contains(
                    WindowsMsiLauncher
                        .RestartApplicationProperty));
    }

    [TestMethod]
    public void RestartApplicationProperty_IsAValidPublicInstallerProperty()
    {
        // Public Windows Installer properties are upper case.
        string name =
            WindowsMsiLauncher
                .RestartApplicationProperty
                .Split('=')[0];

        Assert.AreEqual(
            name.ToUpperInvariant(),
            name);

        Assert.AreEqual(
            "RESTARTCOPYGIF=1",
            WindowsMsiLauncher
                .RestartApplicationProperty);
    }
}
