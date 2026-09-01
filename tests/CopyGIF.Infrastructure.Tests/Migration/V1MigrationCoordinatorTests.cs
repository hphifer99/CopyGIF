using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Infrastructure.Migration;
using CopyGIF.Infrastructure.Storage;
using CopyGIF.Infrastructure.Tests.TestDoubles;

namespace CopyGIF.Infrastructure.Tests.Migration;

[TestClass]
public sealed class V1MigrationCoordinatorTests
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
    public async Task MigrateIfNeededAsync_NoLegacyFiles_MarksNotRequired()
    {
        TestSecretStore secretStore =
            new();

        TestLegacyCredentialDecoder decoder =
            new("unused");

        MigrationHarness harness =
            CreateHarness(
                secretStore,
                decoder);

        MigrationResult result =
            await harness.Coordinator
                .MigrateIfNeededAsync();

        MigrationState state =
            await harness.MigrationStateStore
                .LoadAsync();

        Assert.AreEqual(
            MigrationStatus.NotRequired,
            result.Status);
        Assert.IsTrue(result.Succeeded);
        Assert.IsTrue(state.IsCompleted);
        Assert.IsNotNull(
            state.CompletedAtUtc);
        Assert.IsNull(state.SourceVersion);
        Assert.AreEqual(
            0,
            decoder.DecodeCount);
    }

    [TestMethod]
    public async Task MigrateIfNeededAsync_FullV1Data_MigratesAndArchivesSources()
    {
        TestSecretStore secretStore =
            new();

        TestLegacyCredentialDecoder decoder =
            new("decoded-api-key");

        MigrationHarness harness =
            CreateHarness(
                secretStore,
                decoder);

        (string settingsJson, string libraryJson) =
            await WriteLegacyFilesAsync(
                harness.Paths,
                useProtectedCredential: true);

        MigrationResult result =
            await harness.Coordinator
                .MigrateIfNeededAsync();

        AppSettings settings =
            await harness.SettingsStore
                .LoadAsync();

        LibrarySnapshot library =
            await harness.LibraryStore
                .LoadAsync();

        MigrationState state =
            await harness.MigrationStateStore
                .LoadAsync();

        string? credential =
            await secretStore.GetAsync(
                SecretNames.KlipyApiKey);

        Assert.AreEqual(
            MigrationStatus.Completed,
            result.Status);
        Assert.IsTrue(result.Succeeded);
        Assert.IsTrue(result.MigratedSettings);
        Assert.IsTrue(result.MigratedCredential);
        Assert.AreEqual(
            1,
            result.MigratedFavorites);
        Assert.AreEqual(
            1,
            result.MigratedRecents);
        Assert.AreEqual(
            "Ctrl+Alt+G",
            settings.Hotkey);
        Assert.AreEqual(
            "501",
            library.Favorites.Single()
                .Identity.Id);
        Assert.AreEqual(
            "502",
            library.Recents.Single()
                .Identity.Id);
        Assert.AreEqual(
            "decoded-api-key",
            credential);
        Assert.AreEqual(
            1,
            decoder.DecodeCount);
        Assert.IsTrue(state.IsCompleted);
        Assert.AreEqual(
            "1",
            state.SourceVersion);

        Assert.AreEqual(
            settingsJson,
            await File.ReadAllTextAsync(
                Path.Combine(
                    harness.Paths.MigrationDirectory,
                    "settings.v1.json")));

        Assert.AreEqual(
            libraryJson,
            await File.ReadAllTextAsync(
                Path.Combine(
                    harness.Paths.MigrationDirectory,
                    "library.v1.json")));

        string migratedSettingsJson =
            await File.ReadAllTextAsync(
                harness.Paths.SettingsPath);

        Assert.IsFalse(
            migratedSettingsJson.Contains(
                "decoded-api-key",
                StringComparison.Ordinal));
        Assert.IsFalse(
            migratedSettingsJson.Contains(
                "ApiKeyProtected",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task MigrateIfNeededAsync_CompletedState_DoesNotReadOrChangeLegacyFile()
    {
        TestSecretStore secretStore =
            new();

        TestLegacyCredentialDecoder decoder =
            new("unused");

        MigrationHarness harness =
            CreateHarness(
                secretStore,
                decoder);

        await harness.MigrationStateStore.SaveAsync(
            new MigrationState
            {
                IsCompleted = true,
                CompletedAtUtc =
                    DateTimeOffset.UtcNow,
                SourceVersion = "1"
            });

        const string legacySettings =
            """
            {
              "ApiKeyProtected": "AQIDBA==",
              "Hotkey": "Alt+Z"
            }
            """;

        await File.WriteAllTextAsync(
            harness.Paths.SettingsPath,
            legacySettings);

        MigrationResult result =
            await harness.Coordinator
                .MigrateIfNeededAsync();

        Assert.AreEqual(
            MigrationStatus.NotRequired,
            result.Status);
        Assert.AreEqual(
            legacySettings,
            await File.ReadAllTextAsync(
                harness.Paths.SettingsPath));
        Assert.AreEqual(
            0,
            decoder.DecodeCount);
    }

    [TestMethod]
    public async Task MigrateIfNeededAsync_CredentialWriteFails_RestoresBothV1Files()
    {
        ThrowingSecretStore secretStore =
            new();

        TestLegacyCredentialDecoder decoder =
            new("unused");

        MigrationHarness harness =
            CreateHarness(
                secretStore,
                decoder);

        (string settingsJson, string libraryJson) =
            await WriteLegacyFilesAsync(
                harness.Paths,
                useProtectedCredential: false);

        MigrationResult result =
            await harness.Coordinator
                .MigrateIfNeededAsync();

        MigrationState state =
            await harness.MigrationStateStore
                .LoadAsync();

        Assert.AreEqual(
            MigrationStatus.RolledBack,
            result.Status);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(
            settingsJson,
            await File.ReadAllTextAsync(
                harness.Paths.SettingsPath));
        Assert.AreEqual(
            libraryJson,
            await File.ReadAllTextAsync(
                harness.Paths.LibraryPath));
        Assert.IsFalse(state.IsCompleted);
        Assert.IsTrue(
            secretStore.DeleteWasCalled);
        Assert.IsFalse(
            File.Exists(
                Path.Combine(
                    harness.Paths.MigrationDirectory,
                    "settings.v1.json")));
        Assert.IsFalse(
            File.Exists(
                Path.Combine(
                    harness.Paths.MigrationDirectory,
                    "library.v1.json")));
    }

    [TestMethod]
    public async Task MigrateIfNeededAsync_CredentialDecodeFails_LeavesSourcesUntouched()
    {
        TestSecretStore secretStore =
            new();

        ThrowingLegacyCredentialDecoder decoder =
            new();

        MigrationHarness harness =
            CreateHarness(
                secretStore,
                decoder);

        (string settingsJson, string libraryJson) =
            await WriteLegacyFilesAsync(
                harness.Paths,
                useProtectedCredential: true);

        MigrationResult result =
            await harness.Coordinator
                .MigrateIfNeededAsync();

        Assert.AreEqual(
            MigrationStatus.Failed,
            result.Status);
        Assert.AreEqual(
            settingsJson,
            await File.ReadAllTextAsync(
                harness.Paths.SettingsPath));
        Assert.AreEqual(
            libraryJson,
            await File.ReadAllTextAsync(
                harness.Paths.LibraryPath));
        Assert.IsFalse(
            File.Exists(
                Path.Combine(
                    harness.Paths.MigrationDirectory,
                    "settings.v1.json")));
        Assert.IsFalse(
            File.Exists(
                Path.Combine(
                    harness.Paths.MigrationDirectory,
                    "library.v1.json")));
    }

    [TestMethod]
    public async Task MigrateIfNeededAsync_InterruptedAttempt_RestoresArchivesBeforeRetry()
    {
        TestSecretStore secretStore =
            new();

        TestLegacyCredentialDecoder decoder =
            new("recovered-api-key");

        MigrationHarness harness =
            CreateHarness(
                secretStore,
                decoder);

        (string settingsJson, string libraryJson) =
            await WriteLegacyFilesAsync(
                harness.Paths,
                useProtectedCredential: true);

        string settingsBackupPath =
            Path.Combine(
                harness.Paths.MigrationDirectory,
                "settings.v1.json");

        string libraryBackupPath =
            Path.Combine(
                harness.Paths.MigrationDirectory,
                "library.v1.json");

        await File.WriteAllTextAsync(
            settingsBackupPath,
            settingsJson);

        await File.WriteAllTextAsync(
            libraryBackupPath,
            libraryJson);

        await File.WriteAllTextAsync(
            harness.Paths.SettingsPath,
            "{ partial replacement");

        await File.WriteAllTextAsync(
            harness.Paths.LibraryPath,
            "{ partial replacement");

        MigrationResult result =
            await harness.Coordinator
                .MigrateIfNeededAsync();

        Assert.AreEqual(
            MigrationStatus.Completed,
            result.Status);
        Assert.AreEqual(
            "recovered-api-key",
            await secretStore.GetAsync(
                SecretNames.KlipyApiKey));
        Assert.AreEqual(
            settingsJson,
            await File.ReadAllTextAsync(
                settingsBackupPath));
        Assert.AreEqual(
            libraryJson,
            await File.ReadAllTextAsync(
                libraryBackupPath));
    }

    private MigrationHarness CreateHarness(
        ISecretStore secretStore,
        ILegacyCredentialDecoder decoder)
    {
        ApplicationPaths paths =
            new(_testDirectory);

        VersionedJsonSerializer serializer =
            new(
                new AtomicFileWriter(),
                new CorruptFileRecovery());

        JsonSettingsStore settingsStore =
            new(
                paths,
                serializer);

        JsonLibraryStore libraryStore =
            new(
                paths,
                serializer);

        JsonMigrationStateStore migrationStateStore =
            new(
                paths,
                serializer);

        V1MigrationCoordinator coordinator =
            new(
                paths,
                new V1SettingsReader(),
                new V1LibraryReader(),
                settingsStore,
                libraryStore,
                secretStore,
                decoder,
                migrationStateStore);

        return new MigrationHarness(
            paths,
            settingsStore,
            libraryStore,
            migrationStateStore,
            coordinator);
    }

    private static async Task<(string SettingsJson,
        string LibraryJson)> WriteLegacyFilesAsync(
            ApplicationPaths paths,
            bool useProtectedCredential)
    {
        paths.EnsureDirectoriesExist();

        string credentialProperty =
            useProtectedCredential
                ? "\"ApiKeyProtected\": \"AQIDBA==\""
                : "\"ApiKey\": \"plaintext-api-key\"";

        string settingsJson =
            $$"""
            {
              {{credentialProperty}},
              "Hotkey": "Ctrl+Alt+G",
              "ResultsPerSearch": 30,
              "RecentLimit": 25,
              "FavoriteLimit": 80
            }
            """;

        const string libraryJson =
            """
            {
              "Favorites": [
                {
                  "Id": 501,
                  "Title": "Favorite",
                  "ThumbnailUrl": "https://cdn.example.test/501-small.gif",
                  "FullGifUrl": "https://cdn.example.test/501.gif",
                  "Width": 480,
                  "Height": 270,
                  "AddedUtc": "/Date(1788264000000)/"
                }
              ],
              "Recents": [
                {
                  "Id": 502,
                  "Title": "Recent",
                  "ThumbnailUrl": "https://cdn.example.test/502-small.gif",
                  "FullGifUrl": "https://cdn.example.test/502.gif",
                  "Width": 320,
                  "Height": 180,
                  "AddedUtc": "/Date(1788267600000)/"
                }
              ]
            }
            """;

        await File.WriteAllTextAsync(
            paths.SettingsPath,
            settingsJson);

        await File.WriteAllTextAsync(
            paths.LibraryPath,
            libraryJson);

        return (
            settingsJson,
            libraryJson);
    }

    private sealed record MigrationHarness(
        ApplicationPaths Paths,
        JsonSettingsStore SettingsStore,
        JsonLibraryStore LibraryStore,
        JsonMigrationStateStore MigrationStateStore,
        V1MigrationCoordinator Coordinator);

    private sealed class TestLegacyCredentialDecoder :
        ILegacyCredentialDecoder
    {
        private readonly string _value;

        public TestLegacyCredentialDecoder(
            string value)
        {
            _value = value;
        }

        public int DecodeCount { get; private set; }

        public string DecodeCurrentUserCredential(
            string protectedValue)
        {
            DecodeCount++;

            return _value;
        }
    }

    private sealed class ThrowingLegacyCredentialDecoder :
        ILegacyCredentialDecoder
    {
        public string DecodeCurrentUserCredential(
            string protectedValue)
        {
            throw new InvalidDataException(
                "Test decoder failure.");
        }
    }

    private sealed class ThrowingSecretStore :
        ISecretStore
    {
        public bool DeleteWasCalled { get; private set; }

        public Task<string?> GetAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<string?>(null);
        }

        public Task SetAsync(
            string name,
            string value,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            throw new IOException(
                "Test credential write failure.");
        }

        public Task DeleteAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DeleteWasCalled = true;

            return Task.CompletedTask;
        }
    }
}
