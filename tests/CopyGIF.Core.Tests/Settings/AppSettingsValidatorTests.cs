using CopyGIF.Core.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Core.Tests.Settings;

[TestClass]
public sealed class AppSettingsValidatorTests
{
    [TestMethod]
    public void Validate_DefaultSettings_ReturnsNoIssues()
    {
        IReadOnlyList<SettingsValidationIssue> issues =
            AppSettingsValidator.Validate(
                new AppSettings());

        Assert.AreEqual(
            0,
            issues.Count);
    }

    [TestMethod]
    public void Validate_NullSettings_ReturnsRootIssue()
    {
        IReadOnlyList<SettingsValidationIssue> issues =
            AppSettingsValidator.Validate(
                settings: null);

        Assert.AreEqual(
            1,
            issues.Count);

        Assert.AreEqual(
            "$",
            issues[0].Path);
    }

    [TestMethod]
    public void Validate_NullGroups_ReportsEveryGroup()
    {
        AppSettings settings = new()
        {
            Search = null!,
            Library = null!,
            Window = null!,
            Behavior = null!,
            Appearance = null!,
            Startup = null!,
            Updates = null!,
            Providers = null!
        };

        IReadOnlyList<SettingsValidationIssue> issues =
            AppSettingsValidator.Validate(
                settings);

        string[] paths =
            GetPaths(
                issues);

        Assert.AreEqual(
            8,
            paths.Length);

        CollectionAssert.Contains(
            paths,
            "Search");

        CollectionAssert.Contains(
            paths,
            "Library");

        CollectionAssert.Contains(
            paths,
            "Window");

        CollectionAssert.Contains(
            paths,
            "Behavior");

        CollectionAssert.Contains(
            paths,
            "Appearance");

        CollectionAssert.Contains(
            paths,
            "Startup");

        CollectionAssert.Contains(
            paths,
            "Updates");

        CollectionAssert.Contains(
            paths,
            "Providers");
    }

    [TestMethod]
    public void Validate_InvalidValues_ReportsEveryInvalidPath()
    {
        AppSettings settings = new()
        {
            SchemaVersion = 999,
            Hotkey = "   ",

            Search = new SearchSettings
            {
                ResultsPerSearch =
                    AppSettingsValidator
                        .MinimumResultsPerSearch - 1,

                DebounceMilliseconds =
                    AppSettingsValidator
                        .MinimumDebounceMilliseconds - 1,

                SearchHistoryLimit =
                    AppSettingsValidator
                        .MinimumSearchHistoryLimit - 1
            },

            Library = new LibrarySettings
            {
                RecentLimit =
                    AppSettingsValidator
                        .MinimumRecentLimit - 1,

                FavoriteLimit =
                    AppSettingsValidator
                        .MaximumFavoriteLimit + 1,

                CustomStorageRoot = "   "
            },

            Window = new WindowSettings
            {
                PlacementMode =
                    (WindowPlacementMode)999,

                Width = double.NaN,
                Height = double.PositiveInfinity,

                Left =
                    AppSettingsValidator
                        .MaximumAbsoluteCoordinate + 1,

                Top =
                    -AppSettingsValidator
                        .MaximumAbsoluteCoordinate - 1,

                LastMonitorId = "   "
            },

            Appearance = new AppearanceSettings
            {
                Theme = (AppTheme)999
            },

            Updates = new UpdateSettings
            {
                CheckFrequency =
                    (UpdateCheckFrequency)999,

                Mode = (UpdateMode)999
            },

            Providers = new ProviderSettings
            {
                ActiveProviderId = "   ",

                DisplayMode =
                    (ProviderDisplayMode)999
            }
        };

        IReadOnlyList<SettingsValidationIssue> issues =
            AppSettingsValidator.Validate(
                settings);

        string[] paths =
            GetPaths(
                issues);

        string[] expectedPaths =
        [
            "SchemaVersion",
            "Hotkey",
            "Search.ResultsPerSearch",
            "Search.DebounceMilliseconds",
            "Search.SearchHistoryLimit",
            "Library.RecentLimit",
            "Library.FavoriteLimit",
            "Library.CustomStorageRoot",
            "Window.PlacementMode",
            "Window.Width",
            "Window.Height",
            "Window.Left",
            "Window.Top",
            "Window.LastMonitorId",
            "Appearance.Theme",
            "Updates.CheckFrequency",
            "Updates.Mode",
            "Providers.ActiveProviderId",
            "Providers.DisplayMode"
        ];

        foreach (string expectedPath
                 in expectedPaths)
        {
            CollectionAssert.Contains(
                paths,
                expectedPath);
        }
    }

    [TestMethod]
    public void Validate_BoundaryValues_ReturnsNoIssues()
    {
        AppSettings minimums = new()
        {
            Search = new SearchSettings
            {
                ResultsPerSearch =
                    AppSettingsValidator
                        .MinimumResultsPerSearch,

                DebounceMilliseconds =
                    AppSettingsValidator
                        .MinimumDebounceMilliseconds,

                SearchHistoryLimit =
                    AppSettingsValidator
                        .MinimumSearchHistoryLimit
            },

            Library = new LibrarySettings
            {
                RecentLimit =
                    AppSettingsValidator
                        .MinimumRecentLimit,

                FavoriteLimit =
                    AppSettingsValidator
                        .MinimumFavoriteLimit
            },

            Window = new WindowSettings
            {
                Width =
                    AppSettingsValidator
                        .MinimumWindowWidth,

                Height =
                    AppSettingsValidator
                        .MinimumWindowHeight,

                Left =
                    -AppSettingsValidator
                        .MaximumAbsoluteCoordinate,

                Top =
                    AppSettingsValidator
                        .MaximumAbsoluteCoordinate
            }
        };

        AppSettings maximums = new()
        {
            Hotkey =
                new string(
                    'a',
                    AppSettingsValidator
                        .MaximumHotkeyLength),

            Search = new SearchSettings
            {
                ResultsPerSearch =
                    AppSettingsValidator
                        .MaximumResultsPerSearch,

                DebounceMilliseconds =
                    AppSettingsValidator
                        .MaximumDebounceMilliseconds,

                SearchHistoryLimit =
                    AppSettingsValidator
                        .MaximumSearchHistoryLimit
            },

            Library = new LibrarySettings
            {
                RecentLimit =
                    AppSettingsValidator
                        .MaximumRecentLimit,

                FavoriteLimit =
                    AppSettingsValidator
                        .MaximumFavoriteLimit,

                CustomStorageRoot =
                    new string(
                        'a',
                        AppSettingsValidator
                            .MaximumStorageRootLength)
            },

            Window = new WindowSettings
            {
                Width =
                    AppSettingsValidator
                        .MaximumWindowWidth,

                Height =
                    AppSettingsValidator
                        .MaximumWindowHeight,

                LastMonitorId =
                    new string(
                        'a',
                        AppSettingsValidator
                            .MaximumMonitorIdLength)
            },

            Providers = new ProviderSettings
            {
                ActiveProviderId =
                    new string(
                        'a',
                        AppSettingsValidator
                            .MaximumProviderIdLength)
            }
        };

        Assert.AreEqual(
            0,
            AppSettingsValidator
                .Validate(minimums)
                .Count);

        Assert.AreEqual(
            0,
            AppSettingsValidator
                .Validate(maximums)
                .Count);
    }

    private static string[] GetPaths(
        IEnumerable<SettingsValidationIssue> issues)
    {
        return issues
            .Select(
                issue => issue.Path)
            .ToArray();
    }
}
