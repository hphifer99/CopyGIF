using CopyGIF.Testing;

namespace CopyGIF.Architecture.Tests;

[TestClass]
public sealed class StartupCompositionTests
{
    [TestMethod]
    public void AppStartup_DelegatesToFinalHostBeforeHandlingResult()
    {
        string startupSource =
            ReadSource(
                "src",
                "CopyGIF.App",
                "App.xaml.cs");

        int createHostIndex =
            startupSource.IndexOf(
                "CopyGifHost.Create()",
                StringComparison.Ordinal);

        int startHostIndex =
            startupSource.IndexOf(
                ".StartAsync(",
                StringComparison.Ordinal);

        int handleStatusIndex =
            startupSource.IndexOf(
                "switch (result.Status)",
                StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(
            0,
            createHostIndex,
            "App startup must create the final CopyGifHost composition root.");

        Assert.IsGreaterThan(
            createHostIndex,
            startHostIndex,
            "App startup must start CopyGifHost after creating it.");

        Assert.IsGreaterThan(
            startHostIndex,
            handleStatusIndex,
            "App startup must await CopyGifHost before handling the startup result.");

        Assert.IsFalse(
            startupSource.Contains(
                ".MigrateIfNeededAsync(",
                StringComparison.Ordinal),
            "App must not bypass the final application startup coordinator with a direct migration call.");

        Assert.IsFalse(
            startupSource.Contains(
                "GetRequiredService<MainWindow>",
                StringComparison.Ordinal),
            "App must not resolve or activate MainWindow directly.");

        string repositoryRoot =
            RepositoryRootLocator.Find();

        Assert.IsTrue(
            File.Exists(
                Path.Combine(
                    repositoryRoot,
                    "src",
                    "CopyGIF.App",
                    "StartupFailureWindow.xaml")),
            "The Batch 7 safe-startup failure window must remain available to the final host.");
    }

    [TestMethod]
    public void CopyGifHost_ComposesEveryLayerAndValidatesServices()
    {
        string hostSource =
            ReadSource(
                "src",
                "CopyGIF.App",
                "Composition",
                "CopyGifHost.cs");

        string[] requiredCompositionCalls =
        [
            ".AddCopyGifInfrastructure()",
            ".AddCopyGifWindowsPlatform()",
            ".AddCopyGifApplication()",
            ".AddCopyGifPresentation()",
            "ValidateOnBuild =",
            "ValidateScopes =",
            ".InitializeAsync(",
            "WindowManager",
            "ShellRuntimeState"
        ];

        foreach (string requiredCall
                 in requiredCompositionCalls)
        {
            StringAssert.Contains(
                hostSource,
                requiredCall,
                $"CopyGifHost is missing required composition text: {requiredCall}");
        }

        int infrastructureIndex =
            hostSource.IndexOf(
                ".AddCopyGifInfrastructure()",
                StringComparison.Ordinal);

        int platformIndex =
            hostSource.IndexOf(
                ".AddCopyGifWindowsPlatform()",
                StringComparison.Ordinal);

        int applicationIndex =
            hostSource.IndexOf(
                ".AddCopyGifApplication()",
                StringComparison.Ordinal);

        int presentationIndex =
            hostSource.IndexOf(
                ".AddCopyGifPresentation()",
                StringComparison.Ordinal);

        Assert.IsTrue(
            infrastructureIndex < platformIndex &&
            platformIndex < applicationIndex &&
            applicationIndex < presentationIndex,
            "CopyGifHost must compose Infrastructure, Windows Platform, Application, and Presentation in dependency order.");
    }

    [TestMethod]
    public void AppResources_LoadFinalThemeAndTemplateResources()
    {
        string appXaml =
            ReadSource(
                "src",
                "CopyGIF.App",
                "App.xaml");

        string[] requiredResources =
        [
            "ms-appx:///Resources/Colors.xaml",
            "ms-appx:///Resources/Themes.xaml",
            "ms-appx:///Resources/ControlStyles.xaml",
            "CopyGifGifCardTemplate",
            "CopyGifOnboardingTemplate",
            "CopyGifGeneralSettingsTemplate",
            "CopyGifSearchSettingsTemplate",
            "CopyGifAppearanceSettingsTemplate",
            "CopyGifLibrarySettingsTemplate",
            "CopyGifApiSettingsTemplate",
            "CopyGifUpdatesSettingsTemplate"
        ];

        foreach (string requiredResource
                 in requiredResources)
        {
            StringAssert.Contains(
                appXaml,
                requiredResource,
                $"App.xaml is missing required resource text: {requiredResource}");
        }
    }

    [TestMethod]
    public void PresentationComposition_UsesFinalMainViewModelOnly()
    {
        string presentationComposition =
            ReadSource(
                "src",
                "CopyGIF.Presentation",
                "ServiceCollectionExtensions.cs");

        StringAssert.Contains(
            presentationComposition,
            "using CopyGIF.Presentation.Main;",
            "Presentation composition must import the final MainViewModel namespace.");

        Assert.IsFalse(
            presentationComposition.Contains(
                "CopyGIF.Presentation.ViewModels.MainViewModel",
                StringComparison.Ordinal),
            "Presentation composition must not retain the temporary legacy MainViewModel registration.");
    }

    [TestMethod]
    public void FinalShell_ContainsEveryPlannedProductionFile()
    {
        string repositoryRoot =
            RepositoryRootLocator.Find();

        string[] requiredFiles =
        [
            Path.Combine("src", "CopyGIF.App", "Composition", "CopyGifHost.cs"),
            Path.Combine("src", "CopyGIF.App", "Controls", "GifCard.xaml"),
            Path.Combine("src", "CopyGIF.App", "Controls", "GifCard.xaml.cs"),
            Path.Combine("src", "CopyGIF.App", "Controls", "SearchHeader.xaml"),
            Path.Combine("src", "CopyGIF.App", "Controls", "SearchHeader.xaml.cs"),
            Path.Combine("src", "CopyGIF.App", "Controls", "StatusBanner.xaml"),
            Path.Combine("src", "CopyGIF.App", "Controls", "StatusBanner.xaml.cs"),
            Path.Combine("src", "CopyGIF.App", "Resources", "Colors.xaml"),
            Path.Combine("src", "CopyGIF.App", "Resources", "ControlStyles.xaml"),
            Path.Combine("src", "CopyGIF.App", "Resources", "Themes.xaml"),
            Path.Combine("src", "CopyGIF.App", "Services", "ThemeManager.cs"),
            Path.Combine("src", "CopyGIF.App", "Services", "WindowManager.cs"),
            Path.Combine("src", "CopyGIF.App", "Services", "WinUiDispatcher.cs"),
            Path.Combine("src", "CopyGIF.App", "Views", "MainWindow.xaml"),
            Path.Combine("src", "CopyGIF.App", "Views", "MainWindow.xaml.cs"),
            Path.Combine("src", "CopyGIF.App", "Views", "OnboardingWindow.xaml"),
            Path.Combine("src", "CopyGIF.App", "Views", "OnboardingWindow.xaml.cs"),
            Path.Combine("src", "CopyGIF.App", "Views", "SettingsWindow.xaml"),
            Path.Combine("src", "CopyGIF.App", "Views", "SettingsWindow.xaml.cs"),
            Path.Combine("src", "CopyGIF.App", "Views", "Pages", "FavoritesPage.xaml"),
            Path.Combine("src", "CopyGIF.App", "Views", "Pages", "FavoritesPage.xaml.cs"),
            Path.Combine("src", "CopyGIF.App", "Views", "Pages", "RecentsPage.xaml"),
            Path.Combine("src", "CopyGIF.App", "Views", "Pages", "RecentsPage.xaml.cs"),
            Path.Combine("src", "CopyGIF.App", "Views", "Pages", "SearchPage.xaml"),
            Path.Combine("src", "CopyGIF.App", "Views", "Pages", "SearchPage.xaml.cs")
        ];

        foreach (string requiredFile
                 in requiredFiles)
        {
            Assert.IsTrue(
                File.Exists(
                    Path.Combine(
                        repositoryRoot,
                        requiredFile)),
                $"The final shell file is missing: {requiredFile}");
        }
    }

    [TestMethod]
    public void FinalShell_RemovesSupersededShellFiles()
    {
        string repositoryRoot =
            RepositoryRootLocator.Find();

        string[] supersededFiles =
        [
            Path.Combine("src", "CopyGIF.App", "MainWindow.xaml"),
            Path.Combine("src", "CopyGIF.App", "MainWindow.xaml.cs"),
            Path.Combine("src", "CopyGIF.Presentation", "ViewModels", "MainViewModel.cs"),
            Path.Combine("tests", "CopyGIF.Presentation.Tests", "MainViewModelTests.cs")
        ];

        foreach (string supersededFile
                 in supersededFiles)
        {
            Assert.IsFalse(
                File.Exists(
                    Path.Combine(
                        repositoryRoot,
                        supersededFile)),
                $"The superseded shell file must be removed: {supersededFile}");
        }
    }

    private static string ReadSource(
        params string[] pathParts)
    {
        string repositoryRoot =
            RepositoryRootLocator.Find();

        string path =
            Path.Combine(
                [repositoryRoot, .. pathParts]);

        Assert.IsTrue(
            File.Exists(
                path),
            $"Required source file does not exist: {path}");

        return File.ReadAllText(
            path);
    }
}
