using CopyGIF.Core.Settings;
using CopyGIF.Infrastructure.Migration;

namespace CopyGIF.Infrastructure.Tests.Migration;

[TestClass]
public sealed class V1SettingsReaderTests
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
    public async Task ReadAsync_MissingFile_ReturnsNull()
    {
        V1SettingsReader reader =
            new();

        V1SettingsSnapshot? snapshot =
            await reader.ReadAsync(
                Path.Combine(
                    _testDirectory,
                    "missing.json"));

        Assert.IsNull(snapshot);
    }

    [TestMethod]
    public async Task ReadAsync_V1Fixture_MapsSettingsAndProtectedCredential()
    {
        string protectedValue =
            Convert.ToBase64String(
                [1, 2, 3, 4]);

        string path =
            Path.Combine(
                _testDirectory,
                "settings.json");

        string json =
            $$"""
            {
              "ApiKeyProtected": "{{protectedValue}}",
              "Hotkey": "Ctrl+Shift+G",
              "ResultsPerSearch": 36,
              "SearchDebounceMilliseconds": 450,
              "RecentLimit": 44,
              "FavoriteLimit": 155,
              "WindowPlacementMode": "Remember",
              "CloseWhenFocusLost": false,
              "HideAfterCopy": false,
              "StoreFavoritesLocally": false,
              "StoreRecentsLocally": false,
              "RememberWindowSize": false,
              "AnimatePreviews": false,
              "StartWithWindows": false,
              "AutoLoadMoreResults": true,
              "WindowLeft": 125.5,
              "WindowTop": 85.25,
              "WindowWidth": 900,
              "WindowHeight": 620,
              "HasSavedWindowPlacement": true
            }
            """;

        await File.WriteAllTextAsync(
            path,
            json);

        V1SettingsSnapshot snapshot =
            (await new V1SettingsReader()
                .ReadAsync(path))!;

        Assert.AreEqual(
            "Ctrl+Shift+G",
            snapshot.Settings.Hotkey);
        Assert.AreEqual(
            36,
            snapshot.Settings.Search.ResultsPerSearch);
        Assert.AreEqual(
            450,
            snapshot.Settings.Search.DebounceMilliseconds);
        Assert.IsFalse(
            snapshot.Settings.Search.AnimatePreviews);
        Assert.IsTrue(
            snapshot.Settings.Search.AutoLoadMoreResults);
        Assert.AreEqual(
            44,
            snapshot.Settings.Library.RecentLimit);
        Assert.AreEqual(
            155,
            snapshot.Settings.Library.FavoriteLimit);
        Assert.IsFalse(
            snapshot.Settings.Library.StoreFavoritesLocally);
        Assert.IsFalse(
            snapshot.Settings.Library.StoreRecentsLocally);
        Assert.AreEqual(
            WindowPlacementMode.Remember,
            snapshot.Settings.Window.PlacementMode);
        Assert.IsFalse(
            snapshot.Settings.Window.RememberWindowSize);
        Assert.AreEqual(
            900d,
            snapshot.Settings.Window.Width);
        Assert.AreEqual(
            620d,
            snapshot.Settings.Window.Height);
        Assert.IsTrue(
            snapshot.Settings.Window.Left.HasValue);
        Assert.AreEqual(
            125.5,
            snapshot.Settings.Window.Left.Value);
        Assert.IsTrue(
            snapshot.Settings.Window.Top.HasValue);
        Assert.AreEqual(
            85.25,
            snapshot.Settings.Window.Top.Value);
        Assert.IsFalse(
            snapshot.Settings.Behavior.CloseWhenFocusLost);
        Assert.IsFalse(
            snapshot.Settings.Behavior.HideAfterCopy);
        Assert.IsFalse(
            snapshot.Settings.Startup.StartWithWindows);

        Assert.IsNotNull(
            snapshot.Credential);
        Assert.AreEqual(
            V1CredentialKind.DpapiCurrentUser,
            snapshot.Credential.Kind);
        Assert.AreEqual(
            protectedValue,
            snapshot.Credential.Value);

        Assert.IsTrue(
            snapshot.Settings.Search.ShowTrendingWhenEmpty);
        Assert.IsTrue(
            snapshot.Settings.Search.SaveSearchHistory);
        Assert.AreEqual(
            AppSettings.DefaultProviderId,
            snapshot.Settings.Providers.ActiveProviderId);
    }

    [TestMethod]
    public async Task ReadAsync_PlaintextCredential_TrimsValueAndPreservesSource()
    {
        string path =
            Path.Combine(
                _testDirectory,
                "settings.json");

        const string json =
            """
            {
              "ApiKey": "  test-api-key  ",
              "Hotkey": "Alt+K"
            }
            """;

        await File.WriteAllTextAsync(
            path,
            json);

        V1SettingsSnapshot snapshot =
            (await new V1SettingsReader()
                .ReadAsync(path))!;

        Assert.IsNotNull(
            snapshot.Credential);
        Assert.AreEqual(
            V1CredentialKind.Plaintext,
            snapshot.Credential.Kind);
        Assert.AreEqual(
            "test-api-key",
            snapshot.Credential.Value);
        Assert.AreEqual(
            json,
            await File.ReadAllTextAsync(path));
    }

    [TestMethod]
    public async Task ReadAsync_InvalidProtectedCredential_FallsBackToPlaintextWithWarning()
    {
        string path =
            Path.Combine(
                _testDirectory,
                "settings.json");

        const string json =
            """
            {
              "ApiKeyProtected": "not valid base64!",
              "ApiKey": "fallback-key"
            }
            """;

        await File.WriteAllTextAsync(
            path,
            json);

        V1SettingsSnapshot snapshot =
            (await new V1SettingsReader()
                .ReadAsync(path))!;

        Assert.IsNotNull(
            snapshot.Credential);
        Assert.AreEqual(
            V1CredentialKind.Plaintext,
            snapshot.Credential.Kind);
        Assert.AreEqual(
            "fallback-key",
            snapshot.Credential.Value);
        Assert.AreEqual(
            1,
            snapshot.Warnings.Count);
        Assert.IsFalse(
            snapshot.Warnings.Single()
                .Contains(
                    "fallback-key",
                    StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ReadAsync_VersionedSettings_ThrowsAndPreservesSource()
    {
        string path =
            Path.Combine(
                _testDirectory,
                "settings.json");

        const string json =
            """
            {
              "schemaVersion": 1,
              "hotkey": "Alt+G"
            }
            """;

        await File.WriteAllTextAsync(
            path,
            json);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new V1SettingsReader()
                .ReadAsync(path));

        Assert.AreEqual(
            json,
            await File.ReadAllTextAsync(path));
    }
}
