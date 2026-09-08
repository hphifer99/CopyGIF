using System.Xml.Linq;
using CopyGIF.Testing;

namespace CopyGIF.Architecture.Tests;

[TestClass]
public sealed class WinUiShellStructureTests
{
    private static readonly string[] ExpectedShellFiles =
    [
        "Composition/CopyGifHost.cs",
        "Controls/GifCard.xaml",
        "Controls/GifCard.xaml.cs",
        "Controls/SearchHeader.xaml",
        "Controls/SearchHeader.xaml.cs",
        "Controls/StatusBanner.xaml",
        "Controls/StatusBanner.xaml.cs",
        "Resources/Colors.xaml",
        "Resources/ControlStyles.xaml",
        "Resources/Themes.xaml",
        "Services/ThemeManager.cs",
        "Services/WindowManager.cs",
        "Services/WinUiDispatcher.cs",
        "Views/MainWindow.xaml",
        "Views/MainWindow.xaml.cs",
        "Views/OnboardingWindow.xaml",
        "Views/OnboardingWindow.xaml.cs",
        "Views/SettingsWindow.xaml",
        "Views/SettingsWindow.xaml.cs",
        "Views/Pages/FavoritesPage.xaml",
        "Views/Pages/FavoritesPage.xaml.cs",
        "Views/Pages/RecentsPage.xaml",
        "Views/Pages/RecentsPage.xaml.cs",
        "Views/Pages/SearchPage.xaml",
        "Views/Pages/SearchPage.xaml.cs"
    ];

    [TestMethod]
    public void FinalShell_ContainsEveryFrozenProductionFile()
    {
        string appDirectory =
            GetAppDirectory();

        foreach (string relativePath
                 in ExpectedShellFiles)
        {
            string physicalPath =
                ToPhysicalPath(
                    appDirectory,
                    relativePath);

            Assert.IsTrue(
                File.Exists(
                    physicalPath),
                $"The frozen WinUI shell file is missing: {relativePath}");
        }
    }

    [TestMethod]
    public void AppResources_MergeFrozenDictionariesInDependencyOrder()
    {
        string appXamlPath =
            Path.Combine(
                GetAppDirectory(),
                "App.xaml");

        XDocument appXaml =
            XDocument.Load(
                appXamlPath);

        string[] mergedSources =
            appXaml
                .Descendants()
                .Where(
                    element =>
                        element.Name.LocalName ==
                            "ResourceDictionary")
                .Select(
                    element =>
                        element.Attribute("Source")?.Value)
                .Where(
                    source =>
                        !string.IsNullOrWhiteSpace(
                            source))
                .Cast<string>()
                .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "ms-appx:///Resources/Colors.xaml",
                "ms-appx:///Resources/Themes.xaml",
                "ms-appx:///Resources/ControlStyles.xaml"
            },
            mergedSources,
            "Colors, theme brushes, and control styles must be merged in dependency order.");
    }

    [TestMethod]
    public void MainWindow_ProvidesTheFrozenThreeDestinationNavigation()
    {
        string xaml =
            ReadAppSource(
                "Views",
                "MainWindow.xaml");

        string codeBehind =
            ReadAppSource(
                "Views",
                "MainWindow.xaml.cs");

        string[] requiredXaml =
        [
            "x:Class=\"CopyGIF.App.Views.MainWindow\"",
            "<NavigationView",
            "x:Name=\"SearchNavigationItem\"",
            "x:Name=\"FavoritesNavigationItem\"",
            "x:Name=\"RecentsNavigationItem\"",
            "IsSettingsVisible=\"True\"",
            "<KeyboardAccelerator"
        ];

        foreach (string requiredText
                 in requiredXaml)
        {
            StringAssert.Contains(
                xaml,
                requiredText,
                $"MainWindow is missing required shell text: {requiredText}");
        }

        string[] requiredCodeBehind =
        [
            "MainNavigationDestination.Search",
            "MainNavigationDestination.Favorites",
            "MainNavigationDestination.Recents",
            "SettingsRequested",
            "ShowCurrentDestination()",
            "FocusSearch()"
        ];

        foreach (string requiredText
                 in requiredCodeBehind)
        {
            StringAssert.Contains(
                codeBehind,
                requiredText,
                $"MainWindow code-behind is missing view-navigation text: {requiredText}");
        }
    }

    [TestMethod]
    public void SettingsAndOnboarding_UseDedicatedBoundContentHosts()
    {
        string settingsXaml =
            ReadAppSource(
                "Views",
                "SettingsWindow.xaml");

        string onboardingXaml =
            ReadAppSource(
                "Views",
                "OnboardingWindow.xaml");

        string[] settingsRequirements =
        [
            "x:Name=\"SettingsNavigationView\"",
            "x:Name=\"SectionContentPresenter\"",
            "x:Name=\"GeneralNavigationItem\"",
            "x:Name=\"SearchNavigationItem\"",
            "x:Name=\"AppearanceNavigationItem\"",
            "x:Name=\"LibraryNavigationItem\"",
            "x:Name=\"ApiNavigationItem\"",
            "x:Name=\"UpdatesNavigationItem\"",
            "Command=\"{Binding SaveCommand}\"",
            "Command=\"{Binding CancelCommand}\""
        ];

        foreach (string requiredText
                 in settingsRequirements)
        {
            StringAssert.Contains(
                settingsXaml,
                requiredText,
                $"SettingsWindow is missing required shell text: {requiredText}");
        }

        string[] onboardingRequirements =
        [
            "x:Name=\"StepContentPresenter\"",
            "Content=\"{Binding StepContent}\"",
            "ContentTemplate=\"{Binding StepContentTemplate}\"",
            "Command=\"{Binding BackCommand}\"",
            "Command=\"{Binding NextCommand}\"",
            "Command=\"{Binding FinishCommand}\"",
            "Command=\"{Binding CancelCommand}\""
        ];

        foreach (string requiredText
                 in onboardingRequirements)
        {
            StringAssert.Contains(
                onboardingXaml,
                requiredText,
                $"OnboardingWindow is missing required shell text: {requiredText}");
        }
    }

    [TestMethod]
    public void Pages_ReuseTheFrozenSearchCardAndStatusControls()
    {
        string searchPage =
            ReadAppSource(
                "Views",
                "Pages",
                "SearchPage.xaml");

        StringAssert.Contains(
            searchPage,
            "<controls:SearchHeader");

        string[][] pagePaths =
        [
            ["Views", "Pages", "SearchPage.xaml"],
            ["Views", "Pages", "FavoritesPage.xaml"],
            ["Views", "Pages", "RecentsPage.xaml"]
        ];

        foreach (string[] pagePath
                 in pagePaths)
        {
            string pageSource =
                ReadAppSource(
                    pagePath);

            StringAssert.Contains(
                pageSource,
                "<controls:StatusBanner",
                $"{pagePath[^1]} must reuse the shared StatusBanner control.");

            StringAssert.Contains(
                pageSource,
                "ItemTemplate=\"{Binding ItemTemplate, ElementName=Root}\"",
                $"{pagePath[^1]} must receive the shared GIF card template.");
        }
    }

    [TestMethod]
    public void GifCard_UsesNativeWinUiAnimatedBitmapPlayback()
    {
        string gifCardCodeBehind =
            ReadAppSource(
                "Controls",
                "GifCard.xaml.cs");

        string[] requiredPlaybackText =
        [
            "using Microsoft.UI.Xaml.Media.Imaging;",
            "new BitmapImage",
            "AutoPlay = false",
            "DecodePixelWidth =",
            ".IsAnimatedBitmap",
            ".Play();",
            ".Stop();",
            "_isPointerOver",
            "_hasKeyboardFocus"
        ];

        foreach (string requiredText
                 in requiredPlaybackText)
        {
            StringAssert.Contains(
                gifCardCodeBehind,
                requiredText,
                $"GifCard is missing required native playback text: {requiredText}");
        }

        Assert.IsFalse(
            gifCardCodeBehind.Contains(
                "XamlAnimatedGif",
                StringComparison.OrdinalIgnoreCase),
            "The final WinUI shell must use native BitmapImage playback.");
    }

    [TestMethod]
    public void AppLayer_OwnsFinalNavigationAndComposition()
    {
        string hostSource =
            ReadAppSource(
                "Composition",
                "CopyGifHost.cs");

        string windowManagerSource =
            ReadAppSource(
                "Services",
                "WindowManager.cs");

        string[] hostRequirements =
        [
            "CreateMainWindow",
            "CreateSettingsWindow",
            "CreateOnboardingWindow",
            "BindSearchPage",
            "BindFavoritesPage",
            "BindRecentsPage"
        ];

        foreach (string requiredText
                 in hostRequirements)
        {
            StringAssert.Contains(
                hostSource,
                requiredText,
                $"CopyGifHost is missing final shell composition text: {requiredText}");
        }

        string[] windowManagerRequirements =
        [
            "MainWindow",
            "SettingsWindow",
            "OnboardingWindow",
            "SettingsRequested",
            "Activate()"
        ];

        foreach (string requiredText
                 in windowManagerRequirements)
        {
            StringAssert.Contains(
                windowManagerSource,
                requiredText,
                $"WindowManager is missing final lifecycle text: {requiredText}");
        }
    }

    private static string GetAppDirectory()
    {
        return Path.Combine(
            RepositoryRootLocator.Find(),
            "src",
            "CopyGIF.App");
    }

    private static string ReadAppSource(
        params string[] pathParts)
    {
        string path =
            Path.Combine(
                [GetAppDirectory(), .. pathParts]);

        Assert.IsTrue(
            File.Exists(
                path),
            $"Required WinUI source file does not exist: {path}");

        return File.ReadAllText(
            path);
    }

    private static string ToPhysicalPath(
        string root,
        string relativePath)
    {
        return Path.Combine(
            [root, .. relativePath.Split('/')]);
    }
}
