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
}
