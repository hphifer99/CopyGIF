using CopyGIF.Testing;

namespace CopyGIF.Architecture.Tests;

[TestClass]
public sealed class WinUiAccessibilityTests
{
    [TestMethod]
    public void PrimaryInteractiveSurfaces_DeclareAccessibleNames()
    {
        IReadOnlyDictionary<string, string[]> requiredNames =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Controls/GifCard.xaml"] =
                [
                    "AutomationProperties.Name=\"GIF result\"",
                    "AutomationProperties.Name=\"Add GIF to Favorites\"",
                    "AutomationProperties.Name=\"Copying GIF\""
                ],

                ["Controls/SearchHeader.xaml"] =
                [
                    "AutomationProperties.Name=\"Search GIFs\"",
                    "AutomationProperties.Name=\"Clear search\"",
                    "AutomationProperties.Name=\"Search\"",
                    "AutomationProperties.Name=\"Cancel search\""
                ],

                ["Controls/StatusBanner.xaml"] =
                [
                    "AutomationProperties.Name=\"CopyGIF status\""
                ],

                ["Views/MainWindow.xaml"] =
                [
                    "AutomationProperties.Name=\"CopyGIF navigation\"",
                    "AutomationProperties.Name=\"Search\"",
                    "AutomationProperties.Name=\"Favorites\"",
                    "AutomationProperties.Name=\"Recents\""
                ],

                ["Views/SettingsWindow.xaml"] =
                [
                    "AutomationProperties.Name=\"Settings categories\"",
                    "AutomationProperties.Name=\"Saving settings\"",
                    "AutomationProperties.Name=\"Cancel settings changes\"",
                    "AutomationProperties.Name=\"Save settings\""
                ],

                ["Views/OnboardingWindow.xaml"] =
                [
                    "AutomationProperties.Name=\"Onboarding progress\"",
                    "AutomationProperties.Name=\"Cancel setup\"",
                    "AutomationProperties.Name=\"Previous setup step\"",
                    "AutomationProperties.Name=\"Next setup step\"",
                    "AutomationProperties.Name=\"Finish setup\""
                ],

                ["Views/Pages/SearchPage.xaml"] =
                [
                    "AutomationProperties.Name=\"GIF search results\"",
                    "AutomationProperties.Name=\"Loading GIFs\"",
                    "AutomationProperties.Name=\"Load more GIFs\""
                ],

                ["Views/Pages/FavoritesPage.xaml"] =
                [
                    "AutomationProperties.Name=\"Loading Favorites\"",
                    "AutomationProperties.Name=\"Refresh Favorites\"",
                    "AutomationProperties.Name=\"Favorite GIFs\""
                ],

                ["Views/Pages/RecentsPage.xaml"] =
                [
                    "AutomationProperties.Name=\"Loading Recents\"",
                    "AutomationProperties.Name=\"Refresh Recents\"",
                    "AutomationProperties.Name=\"Clear recent GIF history\"",
                    "AutomationProperties.Name=\"Recently copied GIFs\""
                ]
            };

        foreach (KeyValuePair<string, string[]> fileRequirements
                 in requiredNames)
        {
            string xaml =
                ReadAppSource(
                    fileRequirements.Key);

            foreach (string requiredName
                     in fileRequirements.Value)
            {
                StringAssert.Contains(
                    xaml,
                    requiredName,
                    $"{fileRequirements.Key} is missing accessibility text: {requiredName}");
            }
        }
    }

    [TestMethod]
    public void StatusAndProgressSurfaces_UsePoliteLiveRegions()
    {
        string[] liveRegionFiles =
        [
            "Controls/StatusBanner.xaml",
            "Views/OnboardingWindow.xaml",
            "Views/Pages/SearchPage.xaml",
            "Views/Pages/FavoritesPage.xaml",
            "Views/Pages/RecentsPage.xaml"
        ];

        foreach (string relativePath
                 in liveRegionFiles)
        {
            string xaml =
                ReadAppSource(
                    relativePath);

            StringAssert.Contains(
                xaml,
                "AutomationProperties.LiveSetting=\"Polite\"",
                $"{relativePath} must expose status changes as a polite live region.");
        }

        string appXaml =
            ReadAppSource(
                "App.xaml");

        StringAssert.Contains(
            appXaml,
            "AutomationProperties.LiveSetting=\"Polite\"",
            "Settings and onboarding templates must announce status changes.");
    }

    [TestMethod]
    public void GifCards_SupportKeyboardFocusAndDescriptiveAutomation()
    {
        string gifCardXaml =
            ReadAppSource(
                "Controls/GifCard.xaml");

        string gifCardCodeBehind =
            ReadAppSource(
                "Controls/GifCard.xaml.cs");

        string appXaml =
            ReadAppSource(
                "App.xaml");

        string[] focusRequirements =
        [
            "UseSystemFocusVisuals=\"True\"",
            "GotFocus=\"SelectButton_GotFocus\"",
            "LostFocus=\"SelectButton_LostFocus\"",
            "ToolTipService.ToolTip=\"Toggle Favorite\""
        ];

        foreach (string requiredText
                 in focusRequirements)
        {
            StringAssert.Contains(
                gifCardXaml,
                requiredText,
                $"GifCard is missing keyboard or tooltip text: {requiredText}");
        }

        StringAssert.Contains(
            gifCardCodeBehind,
            "AutomationProperties.SetName(");

        StringAssert.Contains(
            gifCardCodeBehind,
            "AutomationProperties.SetHelpText(");

        StringAssert.Contains(
            appXaml,
            "AutomationProperties.HelpText=\"Press Enter to copy this GIF. Use the Favorite button to change its saved state.\"");
    }

    [TestMethod]
    public void MainShell_ProvidesTheFrozenKeyboardSearchShortcut()
    {
        string mainWindowXaml =
            ReadAppSource(
                "Views/MainWindow.xaml");

        string mainWindowCodeBehind =
            ReadAppSource(
                "Views/MainWindow.xaml.cs");

        string[] acceleratorRequirements =
        [
            "<KeyboardAccelerator",
            "Key=\"F\"",
            "Modifiers=\"Control\"",
            "Invoked=\"FocusSearchKeyboardAccelerator_Invoked\""
        ];

        foreach (string requiredText
                 in acceleratorRequirements)
        {
            StringAssert.Contains(
                mainWindowXaml,
                requiredText,
                $"MainWindow is missing keyboard accelerator text: {requiredText}");
        }

        StringAssert.Contains(
            mainWindowCodeBehind,
            "FocusSearch();");

        StringAssert.Contains(
            mainWindowCodeBehind,
            "eventArgs.Handled =");
    }

    [TestMethod]
    public void DecorativeGifImages_AreExcludedFromTheAutomationTree()
    {
        string gifCardXaml =
            ReadAppSource(
                "Controls/GifCard.xaml");

        int rawImageCount =
            CountOccurrences(
                gifCardXaml,
                "AutomationProperties.AccessibilityView=\"Raw\"");

        Assert.AreEqual(
            2,
            rawImageCount,
            "The thumbnail and animated preview must remain decorative automation elements.");
    }

    [TestMethod]
    public void ThemeSystem_PreservesHighContrastAndReducedAnimation()
    {
        string themesXaml =
            ReadAppSource(
                "Resources/Themes.xaml");

        string themeManagerSource =
            ReadAppSource(
                "Services/ThemeManager.cs");

        string hostSource =
            ReadAppSource(
                "Composition/CopyGifHost.cs");

        string[] highContrastRequirements =
        [
            "x:Key=\"HighContrast\"",
            "SystemColorHighlightColor",
            "SystemColorWindowColor",
            "SystemColorWindowTextColor"
        ];

        foreach (string requiredText
                 in highContrastRequirements)
        {
            StringAssert.Contains(
                themesXaml,
                requiredText,
                $"Themes.xaml is missing high-contrast text: {requiredText}");
        }

        string[] managerRequirements =
        [
            "AccessibilitySettings",
            "HighContrastChanged",
            "UISettings",
            "AnimationsEnabledChanged",
            "public bool AnimationsEnabled",
            "public bool IsHighContrast",
            "return ElementTheme.Default;"
        ];

        foreach (string requiredText
                 in managerRequirements)
        {
            StringAssert.Contains(
                themeManagerSource,
                requiredText,
                $"ThemeManager is missing accessibility behavior text: {requiredText}");
        }

        StringAssert.Contains(
            hostSource,
            "_mainViewModel.ReducedMotion =");

        StringAssert.Contains(
            hostSource,
            "!_themeManager.AnimationsEnabled");
    }

    private static string ReadAppSource(
        string relativePath)
    {
        string appDirectory =
            Path.Combine(
                RepositoryRootLocator.Find(),
                "src",
                "CopyGIF.App");

        string path =
            Path.Combine(
                [appDirectory, .. relativePath.Split('/')]);

        Assert.IsTrue(
            File.Exists(
                path),
            $"Required WinUI source file does not exist: {path}");

        return File.ReadAllText(
            path);
    }

    private static int CountOccurrences(
        string source,
        string value)
    {
        int count = 0;
        int searchIndex = 0;

        while (true)
        {
            int matchIndex =
                source.IndexOf(
                    value,
                    searchIndex,
                    StringComparison.Ordinal);

            if (matchIndex < 0)
            {
                return count;
            }

            count++;

            searchIndex =
                matchIndex +
                value.Length;
        }
    }
}
