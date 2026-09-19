using CopyGIF.Core.Settings;
using CopyGIF.Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Infrastructure.Tests.Storage;

[TestClass]
public sealed class JsonSettingsStoreTests
{
    private string _testDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _testDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "CopyGIF.Tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            _testDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(
                    _testDirectory))
            {
                Directory.Delete(
                    _testDirectory,
                    recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [TestMethod]
    public async Task LoadAsync_NoSettingsFile_ReturnsDefaults()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        JsonSettingsStore store =
            new(paths);

        AppSettings result =
            await store.LoadAsync();

        Assert.AreEqual(
            "Alt+G",
            result.Hotkey);

        Assert.AreEqual(
            24,
            result.Search.ResultsPerSearch);

        Assert.AreEqual(
            760d,
            result.Window.Width);
    }

    [TestMethod]
    public async Task SaveThenLoadAsync_RoundTripsSettings()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        JsonSettingsStore store =
            new(paths);

        AppSettings expected = new()
        {
            Hotkey = "Ctrl+Shift+G",

            Search = new SearchSettings
            {
                ResultsPerSearch = 36,
                DebounceMilliseconds = 500,
                AnimatePreviews = false
            },

            Window = new WindowSettings
            {
                Width = 900,
                Height = 650
            }
        };

        await store.SaveAsync(expected);

        AppSettings actual =
            await store.LoadAsync();

        Assert.IsTrue(
            File.Exists(paths.SettingsPath));

        Assert.AreEqual(
            "Ctrl+Shift+G",
            actual.Hotkey);

        Assert.AreEqual(
            36,
            actual.Search.ResultsPerSearch);

        Assert.AreEqual(
            500,
            actual.Search.DebounceMilliseconds);

        Assert.IsFalse(
            actual.Search.AnimatePreviews);

        Assert.AreEqual(
            900d,
            actual.Window.Width);

        Assert.AreEqual(
            650d,
            actual.Window.Height);
    }

    [TestMethod]
    public async Task SeparateStoreInstances_ConcurrentReadsAndWrites_NeverQuarantineValidSettings()
    {
        ApplicationPaths paths = new(_testDirectory);
        JsonSettingsStore first = new(paths);
        JsonSettingsStore second = new(paths);
        await first.SaveAsync(new AppSettings());

        Task writer = Task.Run(async () =>
        {
            for (int i = 0; i < 30; i++)
                await first.SaveAsync(new AppSettings { Hotkey = i % 2 == 0 ? "Alt+G" : "Ctrl+G" });
        });
        Task reader = Task.Run(async () =>
        {
            for (int i = 0; i < 30; i++)
            {
                AppSettings current = await second.LoadAsync();
                Assert.IsTrue(current.Hotkey is "Alt+G" or "Ctrl+G");
            }
        });
        await Task.WhenAll(writer, reader);

        Assert.IsEmpty(Directory.GetFiles(_testDirectory, "settings.json.corrupt.*"));
        Assert.IsTrue((await second.LoadAsync()).Hotkey is "Alt+G" or "Ctrl+G");
    }

    [TestMethod]
    public async Task LoadAsync_FileLocked_DoesNotReplaceTheSettingsWithBackup()
    {
        ApplicationPaths paths = new(_testDirectory);
        JsonSettingsStore store = new(paths);
        await store.SaveAsync(new AppSettings { Hotkey = "Ctrl+G" });
        using (FileStream exclusive = new(paths.SettingsPath, FileMode.Open,
            FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAsync<IOException>(() => store.LoadAsync());
        }

        Assert.AreEqual("Ctrl+G", (await store.LoadAsync()).Hotkey);
        Assert.IsEmpty(Directory.GetFiles(_testDirectory, "settings.json.corrupt.*"));
    }

    [TestMethod]
    public async Task LoadAsync_CorruptPrimary_UsesBackup()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        JsonSettingsStore store =
            new(paths);

        AppSettings firstVersion = new()
        {
            Hotkey = "Alt+G",

            Search = new SearchSettings
            {
                ResultsPerSearch = 30
            }
        };

        AppSettings secondVersion = new()
        {
            Hotkey = "Ctrl+Alt+G",

            Search = new SearchSettings
            {
                ResultsPerSearch = 40
            }
        };

        await store.SaveAsync(
            firstVersion);

        await store.SaveAsync(
            secondVersion);

        await File.WriteAllTextAsync(
            paths.SettingsPath,
            "{ this is not valid json");

        AppSettings recovered =
            await store.LoadAsync();

        Assert.AreEqual(
            "Alt+G",
            recovered.Hotkey);

        Assert.AreEqual(
            30,
            recovered.Search.ResultsPerSearch);
    }

    [TestMethod]
    public async Task SaveAsync_DoesNotSerializeCredentials()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        JsonSettingsStore store =
            new(paths);

        await store.SaveAsync(
            new AppSettings());

        string json =
            await File.ReadAllTextAsync(
                paths.SettingsPath);

        Assert.IsFalse(
            json.Contains(
                "apiKey",
                StringComparison.OrdinalIgnoreCase));

        Assert.IsFalse(
            json.Contains(
                "password",
                StringComparison.OrdinalIgnoreCase));

        Assert.IsFalse(
            json.Contains(
                "token",
                StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task LoadAsync_LegacySettings_ThrowsWithoutModifyingFile()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        const string legacyJson =
            """
        {
          "Hotkey": "Alt+G",
          "ResultsPerSearch": 24,
          "RecentLimit": 30,
          "FavoriteLimit": 100
        }
        """;

        await File.WriteAllTextAsync(
            paths.SettingsPath,
            legacyJson);

        JsonSettingsStore store =
            new(paths);

        await Assert.ThrowsAsync<
            LegacySettingsDetectedException>(
            () => store.LoadAsync());

        string after =
            await File.ReadAllTextAsync(
                paths.SettingsPath);

        Assert.AreEqual(
            legacyJson,
            after);
    }

    [TestMethod]
    public async Task SaveAsync_LegacySettings_RefusesToOverwriteFile()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        const string legacyJson =
            """
        {
          "Hotkey": "Alt+G",
          "ResultsPerSearch": 24
        }
        """;

        await File.WriteAllTextAsync(
            paths.SettingsPath,
            legacyJson);

        JsonSettingsStore store =
            new(paths);

        await Assert.ThrowsAsync<
            LegacySettingsDetectedException>(
            () => store.SaveAsync(
                new AppSettings()));

        string after =
            await File.ReadAllTextAsync(
                paths.SettingsPath);

        Assert.AreEqual(
            legacyJson,
            after);
    }
}
