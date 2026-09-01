using CopyGIF.Core.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Core.Tests.Settings;

[TestClass]
public sealed class AppSettingsNormalizerTests
{
    [TestMethod]
    public void Normalize_NullSettings_ReturnsEveryFrozenDefault()
    {
        AppSettings result =
            AppSettingsNormalizer.Normalize(
                settings: null);

        Assert.AreEqual(
            AppSettings.CurrentSchemaVersion,
            result.SchemaVersion);

        Assert.AreEqual(
            AppSettings.DefaultHotkey,
            result.Hotkey);

        Assert.AreEqual(
            24,
            result.Search.ResultsPerSearch);

        Assert.AreEqual(
            300,
            result.Search.DebounceMilliseconds);

        Assert.IsTrue(
            result.Search.AnimatePreviews);

        Assert.IsFalse(
            result.Search.AutoLoadMoreResults);

        Assert.IsTrue(
            result.Search.ShowTrendingWhenEmpty);

        Assert.IsTrue(
            result.Search.SaveSearchHistory);

        Assert.IsTrue(
            result.Search.UseHistorySuggestions);

        Assert.AreEqual(
            50,
            result.Search.SearchHistoryLimit);

        Assert.AreEqual(
            30,
            result.Library.RecentLimit);

        Assert.AreEqual(
            100,
            result.Library.FavoriteLimit);

        Assert.IsTrue(
            result.Library.StoreFavoritesLocally);

        Assert.IsTrue(
            result.Library.StoreRecentsLocally);

        Assert.IsNull(
            result.Library.CustomStorageRoot);

        Assert.AreEqual(
            WindowPlacementMode.Mouse,
            result.Window.PlacementMode);

        Assert.IsTrue(
            result.Window.RememberWindowSize);

        Assert.AreEqual(
            760d,
            result.Window.Width);

        Assert.AreEqual(
            560d,
            result.Window.Height);

        Assert.IsNull(
            result.Window.Left);

        Assert.IsNull(
            result.Window.Top);

        Assert.IsNull(
            result.Window.LastMonitorId);

        Assert.IsTrue(
            result.Behavior.CloseWhenFocusLost);

        Assert.IsTrue(
            result.Behavior.HideAfterCopy);

        Assert.AreEqual(
            AppTheme.System,
            result.Appearance.Theme);

        Assert.IsTrue(
            result.Startup.StartWithWindows);

        Assert.IsTrue(
            result.Updates.CheckForUpdates);

        Assert.AreEqual(
            UpdateCheckFrequency.Daily,
            result.Updates.CheckFrequency);

        Assert.AreEqual(
            UpdateMode.Recommended,
            result.Updates.Mode);

        Assert.AreEqual(
            AppSettings.DefaultProviderId,
            result.Providers.ActiveProviderId);

        Assert.AreEqual(
            ProviderDisplayMode.Single,
            result.Providers.DisplayMode);
    }

    [TestMethod]
    public void Normalize_InvalidValues_RestoresSafeDefaults()
    {
        AppSettings settings = new()
        {
            SchemaVersion = 999,

            Hotkey =
                new string(
                    'x',
                    AppSettingsValidator
                        .MaximumHotkeyLength + 1),

            Search = new SearchSettings
            {
                ResultsPerSearch = 999,
                DebounceMilliseconds = 1,
                SearchHistoryLimit = 0
            },

            Library = new LibrarySettings
            {
                RecentLimit = 0,
                FavoriteLimit = 9999,
                CustomStorageRoot = "   "
            },

            Window = new WindowSettings
            {
                PlacementMode =
                    (WindowPlacementMode)999,

                Width = 1,
                Height = double.NaN,
                Left = double.PositiveInfinity,
                Top =
                    AppSettingsValidator
                        .MaximumAbsoluteCoordinate + 1,

                LastMonitorId = "Monitor\u0001"
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
                ActiveProviderId = "bad\u0001",
                DisplayMode =
                    (ProviderDisplayMode)999
            }
        };

        AppSettings result =
            AppSettingsNormalizer.Normalize(
                settings);

        Assert.AreEqual(
            AppSettings.CurrentSchemaVersion,
            result.SchemaVersion);

        Assert.AreEqual(
            AppSettings.DefaultHotkey,
            result.Hotkey);

        Assert.AreEqual(
            24,
            result.Search.ResultsPerSearch);

        Assert.AreEqual(
            300,
            result.Search.DebounceMilliseconds);

        Assert.AreEqual(
            50,
            result.Search.SearchHistoryLimit);

        Assert.AreEqual(
            30,
            result.Library.RecentLimit);

        Assert.AreEqual(
            100,
            result.Library.FavoriteLimit);

        Assert.IsNull(
            result.Library.CustomStorageRoot);

        Assert.AreEqual(
            WindowPlacementMode.Mouse,
            result.Window.PlacementMode);

        Assert.AreEqual(
            760d,
            result.Window.Width);

        Assert.AreEqual(
            560d,
            result.Window.Height);

        Assert.IsNull(
            result.Window.Left);

        Assert.IsNull(
            result.Window.Top);

        Assert.IsNull(
            result.Window.LastMonitorId);

        Assert.AreEqual(
            AppTheme.System,
            result.Appearance.Theme);

        Assert.AreEqual(
            UpdateCheckFrequency.Daily,
            result.Updates.CheckFrequency);

        Assert.AreEqual(
            UpdateMode.Recommended,
            result.Updates.Mode);

        Assert.AreEqual(
            AppSettings.DefaultProviderId,
            result.Providers.ActiveProviderId);

        Assert.AreEqual(
            ProviderDisplayMode.Single,
            result.Providers.DisplayMode);
    }

    [TestMethod]
    public void Normalize_ValidValues_PreservesAndTrimsValues()
    {
        AppSettings settings = new()
        {
            Hotkey = "  Ctrl+Alt+G  ",

            Search = new SearchSettings
            {
                ResultsPerSearch = 36,
                DebounceMilliseconds = 450,
                AnimatePreviews = false,
                AutoLoadMoreResults = true,
                ShowTrendingWhenEmpty = false,
                SaveSearchHistory = false,
                UseHistorySuggestions = false,
                SearchHistoryLimit = 75
            },

            Library = new LibrarySettings
            {
                RecentLimit = 50,
                FavoriteLimit = 250,
                StoreFavoritesLocally = false,
                StoreRecentsLocally = false,
                CustomStorageRoot = "  C:\\GIFStorage  "
            },

            Window = new WindowSettings
            {
                PlacementMode =
                    WindowPlacementMode.Remember,

                RememberWindowSize = false,
                Width = 1000,
                Height = 700,
                Left = 100,
                Top = 200,
                LastMonitorId = "  Monitor-2  "
            },

            Behavior = new BehaviorSettings
            {
                CloseWhenFocusLost = false,
                HideAfterCopy = false
            },

            Appearance = new AppearanceSettings
            {
                Theme = AppTheme.Dark
            },

            Startup = new StartupSettings
            {
                StartWithWindows = false
            },

            Updates = new UpdateSettings
            {
                CheckForUpdates = false,
                CheckFrequency =
                    UpdateCheckFrequency.Weekly,

                Mode =
                    UpdateMode.DownloadAndInstall
            },

            Providers = new ProviderSettings
            {
                ActiveProviderId = "  KLIPY  ",
                DisplayMode =
                    ProviderDisplayMode.Combined
            }
        };

        AppSettings result =
            AppSettingsNormalizer.Normalize(
                settings);

        Assert.AreEqual(
            "Ctrl+Alt+G",
            result.Hotkey);

        Assert.AreEqual(
            36,
            result.Search.ResultsPerSearch);

        Assert.AreEqual(
            450,
            result.Search.DebounceMilliseconds);

        Assert.IsFalse(
            result.Search.AnimatePreviews);

        Assert.IsTrue(
            result.Search.AutoLoadMoreResults);

        Assert.IsFalse(
            result.Search.ShowTrendingWhenEmpty);

        Assert.IsFalse(
            result.Search.SaveSearchHistory);

        Assert.IsFalse(
            result.Search.UseHistorySuggestions);

        Assert.AreEqual(
            75,
            result.Search.SearchHistoryLimit);

        Assert.AreEqual(
            50,
            result.Library.RecentLimit);

        Assert.AreEqual(
            250,
            result.Library.FavoriteLimit);

        Assert.IsFalse(
            result.Library.StoreFavoritesLocally);

        Assert.IsFalse(
            result.Library.StoreRecentsLocally);

        Assert.AreEqual(
            "C:\\GIFStorage",
            result.Library.CustomStorageRoot);

        Assert.AreEqual(
            WindowPlacementMode.Remember,
            result.Window.PlacementMode);

        Assert.IsFalse(
            result.Window.RememberWindowSize);

        Assert.AreEqual(
            1000d,
            result.Window.Width);

        Assert.AreEqual(
            700d,
            result.Window.Height);

        Assert.AreEqual(
            100d,
            result.Window.Left);

        Assert.AreEqual(
            200d,
            result.Window.Top);

        Assert.AreEqual(
            "Monitor-2",
            result.Window.LastMonitorId);

        Assert.IsFalse(
            result.Behavior.CloseWhenFocusLost);

        Assert.IsFalse(
            result.Behavior.HideAfterCopy);

        Assert.AreEqual(
            AppTheme.Dark,
            result.Appearance.Theme);

        Assert.IsFalse(
            result.Startup.StartWithWindows);

        Assert.IsFalse(
            result.Updates.CheckForUpdates);

        Assert.AreEqual(
            UpdateCheckFrequency.Weekly,
            result.Updates.CheckFrequency);

        Assert.AreEqual(
            UpdateMode.DownloadAndInstall,
            result.Updates.Mode);

        Assert.AreEqual(
            "klipy",
            result.Providers.ActiveProviderId);

        Assert.AreEqual(
            ProviderDisplayMode.Combined,
            result.Providers.DisplayMode);
    }

    [TestMethod]
    public void Normalize_NullGroups_RestoresCompleteGroups()
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

        AppSettings result =
            AppSettingsNormalizer.Normalize(
                settings);

        Assert.IsNotNull(
            result.Search);

        Assert.IsNotNull(
            result.Library);

        Assert.IsNotNull(
            result.Window);

        Assert.IsNotNull(
            result.Behavior);

        Assert.IsNotNull(
            result.Appearance);

        Assert.IsNotNull(
            result.Startup);

        Assert.IsNotNull(
            result.Updates);

        Assert.IsNotNull(
            result.Providers);

        Assert.AreEqual(
            24,
            result.Search.ResultsPerSearch);

        Assert.AreEqual(
            AppTheme.System,
            result.Appearance.Theme);

        Assert.AreEqual(
            UpdateMode.Recommended,
            result.Updates.Mode);
    }

    [TestMethod]
    public void Normalize_OutputAlwaysPassesValidation()
    {
        AppSettings invalidSettings = new()
        {
            SchemaVersion = -1,
            Hotkey = "   ",
            Search = null!,
            Library = null!,
            Window = null!,
            Behavior = null!,
            Appearance = null!,
            Startup = null!,
            Updates = null!,
            Providers = null!
        };

        AppSettings normalized =
            AppSettingsNormalizer.Normalize(
                invalidSettings);

        IReadOnlyList<SettingsValidationIssue> issues =
            AppSettingsValidator.Validate(
                normalized);

        Assert.AreEqual(
            0,
            issues.Count);
    }
}
