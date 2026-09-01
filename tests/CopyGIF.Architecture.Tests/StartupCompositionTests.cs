using CopyGIF.Testing;

namespace CopyGIF.Architecture.Tests;

[TestClass]
public sealed class StartupCompositionTests
{
    [TestMethod]
    public void AppStartup_RunsMigrationBeforeResolvingMainWindow()
    {
        string startupSource =
            ReadAppStartupSource();

        int launchMethodIndex =
            startupSource.IndexOf(
                "protected override async void OnLaunched",
                StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(
            0,
            launchMethodIndex,
            "App startup must provide an asynchronous launch gate.");

        string launchSource =
            startupSource[launchMethodIndex..];

        int migrationIndex =
            launchSource.IndexOf(
                ".MigrateIfNeededAsync()",
                StringComparison.Ordinal);

        int mainWindowIndex =
            launchSource.IndexOf(
                "MainWindow>();",
                StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(
            0,
            migrationIndex,
            "App startup must run legacy migration.");

        Assert.IsGreaterThan(
            migrationIndex,
            mainWindowIndex,
            "Migration must complete before MainWindow is resolved.");
    }

    [TestMethod]
    public void AppStartup_BlocksMainWindowWhenMigrationIsUnsafe()
    {
        string startupSource =
            ReadAppStartupSource();

        StringAssert.Contains(
            startupSource,
            "if (!migrationResult.Succeeded)");

        StringAssert.Contains(
            startupSource,
            "ShowStartupFailure(");

        string repositoryRoot =
            RepositoryRootLocator.Find();

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "CopyGIF.App",
                    "StartupFailureWindow.xaml")),
            "The safe startup failure window is missing.");
    }

    private static string ReadAppStartupSource()
    {
        string repositoryRoot =
            RepositoryRootLocator.Find();

        return File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "CopyGIF.App",
                "App.xaml.cs"));
    }
}
