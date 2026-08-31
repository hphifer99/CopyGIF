using CopyGIF.Core.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Core.Tests.Settings;

[TestClass]
public sealed class AppSettingsNormalizerTests
{
    [TestMethod]
    public void Normalize_NullSettings_ReturnsDefaults()
    {
        AppSettings result =
            AppSettingsNormalizer.Normalize(null);

        Assert.AreEqual(
            AppSettings.CurrentSchemaVersion,
            result.SchemaVersion);

        Assert.AreEqual(
            "Alt+G",
            result.Hotkey);

        Assert.AreEqual(
            24,
            result.Search.ResultsPerSearch);

        Assert.AreEqual(
            300,
            result.Search.DebounceMilliseconds);

        Assert.AreEqual(
            30,
            result.Library.RecentLimit);

        Assert.AreEqual(
            100,
            result.Library.FavoriteLimit);

        Assert.AreEqual(
            760d,
            result.Window.Width);

        Assert.AreEqual(
            560d,
            result.Window.Height);

        Assert.IsTrue(
            result.Search.AnimatePreviews);

        Assert.IsTrue(
            result.Startup.StartWithWindows);
    }

    [TestMethod]
    public void Normalize_InvalidValues_RestoresSafeDefaults()
    {
        AppSettings settings = new()
        {
            Hotkey = "   ",

            Search = new SearchSettings
            {
                ResultsPerSearch = 999,
                DebounceMilliseconds = 1
            },

            Library = new LibrarySettings
            {
                RecentLimit = 0,
                FavoriteLimit = 9999
            },

            Window = new WindowSettings
            {
                Width = 1,
                Height = double.NaN,
                Left = double.PositiveInfinity,
                Top = 10_000_001
            }
        };

        AppSettings result =
            AppSettingsNormalizer.Normalize(settings);

        Assert.AreEqual(
            "Alt+G",
            result.Hotkey);

        Assert.AreEqual(
            24,
            result.Search.ResultsPerSearch);

        Assert.AreEqual(
            300,
            result.Search.DebounceMilliseconds);

        Assert.AreEqual(
            30,
            result.Library.RecentLimit);

        Assert.AreEqual(
            100,
            result.Library.FavoriteLimit);

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
    }

    [TestMethod]
    public void Normalize_ValidValues_PreservesValues()
    {
        AppSettings settings = new()
        {
            Hotkey = "Ctrl+Alt+G",

            Search = new SearchSettings
            {
                ResultsPerSearch = 36,
                DebounceMilliseconds = 450,
                AnimatePreviews = false,
                AutoLoadMoreResults = true
            },

            Library = new LibrarySettings
            {
                RecentLimit = 50,
                FavoriteLimit = 250
            },

            Window = new WindowSettings
            {
                Width = 1000,
                Height = 700,
                Left = 100,
                Top = 200
            }
        };

        AppSettings result =
            AppSettingsNormalizer.Normalize(settings);

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

        Assert.AreEqual(
            50,
            result.Library.RecentLimit);

        Assert.AreEqual(
            250,
            result.Library.FavoriteLimit);

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
    }
}