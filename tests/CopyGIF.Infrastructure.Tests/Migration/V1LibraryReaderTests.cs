using System.Text.Json;
using CopyGIF.Core.Settings;
using CopyGIF.Infrastructure.Migration;

namespace CopyGIF.Infrastructure.Tests.Migration;

[TestClass]
public sealed class V1LibraryReaderTests
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
        V1LibraryReader reader =
            new();

        V1LibrarySnapshot? snapshot =
            await reader.ReadAsync(
                Path.Combine(
                    _testDirectory,
                    "missing.json"));

        Assert.IsNull(snapshot);
    }

    [TestMethod]
    public async Task ReadAsync_V1Fixture_MapsFavoriteAndRecent()
    {
        string localPath =
            Path.Combine(
                _testDirectory,
                "Favorites",
                "42.gif");

        string json =
            JsonSerializer.Serialize(
                new
                {
                    Favorites = new[]
                    {
                        new
                        {
                            Id = 42L,
                            Title = "  Favorite GIF  ",
                            ThumbnailUrl =
                                "https://cdn.example.test/42-small.gif",
                            FullGifUrl =
                                "https://cdn.example.test/42.gif",
                            PreviewGifUrl =
                                "https://cdn.example.test/42-preview.gif",
                            Width = 480,
                            Height = 270,
                            LocalFilePath = localPath,
                            AddedUtc =
                                "/Date(1788264000000)/"
                        }
                    },
                    Recents = new[]
                    {
                        new
                        {
                            Id = 99L,
                            Title = "Recent GIF",
                            ThumbnailUrl =
                                "https://cdn.example.test/99-small.gif",
                            FullGifUrl =
                                "https://cdn.example.test/99.gif",
                            PreviewGifUrl =
                                (string?)null,
                            Width = 320,
                            Height = 180,
                            LocalFilePath =
                                (string?)null,
                            AddedUtc =
                                "/Date(1788267600000+0000)/"
                        }
                    }
                });

        string path =
            Path.Combine(
                _testDirectory,
                "library.json");

        await File.WriteAllTextAsync(
            path,
            json);

        V1LibrarySnapshot snapshot =
            (await new V1LibraryReader()
                .ReadAsync(path))!;

        var favorite =
            snapshot.Library.Favorites.Single();

        Assert.AreEqual(
            AppSettings.DefaultProviderId,
            favorite.Identity.ProviderId);
        Assert.AreEqual(
            "42",
            favorite.Identity.Id);
        Assert.AreEqual(
            "Favorite GIF",
            favorite.Title);
        Assert.AreEqual(
            "https://cdn.example.test/42.gif",
            favorite.GifUri.AbsoluteUri);
        Assert.AreEqual(
            "https://cdn.example.test/42-preview.gif",
            favorite.PreviewUri?.AbsoluteUri);
        Assert.AreEqual(
            Path.GetFullPath(localPath),
            favorite.LocalFilePath);
        Assert.AreEqual(
            DateTimeOffset.FromUnixTimeMilliseconds(
                1788264000000),
            favorite.AddedAtUtc);
        Assert.IsNull(
            favorite.LastCopiedAtUtc);
        Assert.AreEqual(
            0,
            favorite.CopyCount);

        var recent =
            snapshot.Library.Recents.Single();

        Assert.AreEqual(
            "99",
            recent.Identity.Id);
        Assert.AreEqual(
            DateTimeOffset.FromUnixTimeMilliseconds(
                1788267600000),
            recent.AddedAtUtc);
        Assert.IsTrue(
            recent.LastCopiedAtUtc.HasValue);
        Assert.AreEqual(
            recent.AddedAtUtc,
            recent.LastCopiedAtUtc.Value);
        Assert.AreEqual(
            1,
            recent.CopyCount);
        Assert.AreEqual(
            0,
            snapshot.Warnings.Count);
    }

    [TestMethod]
    public async Task ReadAsync_ZeroIdsForSameUrl_ProduceSameDeterministicIdentity()
    {
        const string json =
            """
            {
              "Favorites": [
                {
                  "Id": 0,
                  "FullGifUrl": "https://cdn.example.test/shared.gif",
                  "ThumbnailUrl": "https://cdn.example.test/shared-small.gif",
                  "AddedUtc": "/Date(1788264000000)/"
                }
              ],
              "Recents": [
                {
                  "Id": 0,
                  "FullGifUrl": "https://cdn.example.test/shared.gif",
                  "ThumbnailUrl": "https://cdn.example.test/shared-small.gif",
                  "AddedUtc": "/Date(1788267600000)/"
                }
              ]
            }
            """;

        string path =
            Path.Combine(
                _testDirectory,
                "library.json");

        await File.WriteAllTextAsync(
            path,
            json);

        V1LibrarySnapshot snapshot =
            (await new V1LibraryReader()
                .ReadAsync(path))!;

        string favoriteId =
            snapshot.Library.Favorites.Single()
                .Identity.Id;

        string recentId =
            snapshot.Library.Recents.Single()
                .Identity.Id;

        Assert.AreEqual(
            favoriteId,
            recentId);
        StringAssert.StartsWith(
            favoriteId,
            "url-");
        Assert.AreEqual(
            68,
            favoriteId.Length);
    }

    [TestMethod]
    public async Task ReadAsync_InvalidEntries_AreSkippedWithoutLeakingUrls()
    {
        const string privateUrl =
            "http://private.example.test/secret.gif";

        string json =
            $$"""
            {
              "Favorites": [
                {
                  "Id": 1,
                  "FullGifUrl": "{{privateUrl}}",
                  "ThumbnailUrl": "https://cdn.example.test/one.gif",
                  "AddedUtc": "/Date(1788264000000)/"
                }
              ],
              "Recents": [
                {
                  "Id": 2,
                  "FullGifUrl": "https://cdn.example.test/two.gif",
                  "ThumbnailUrl": "https://cdn.example.test/two-small.gif",
                  "AddedUtc": "not-a-date"
                }
              ]
            }
            """;

        string path =
            Path.Combine(
                _testDirectory,
                "library.json");

        await File.WriteAllTextAsync(
            path,
            json);

        V1LibrarySnapshot snapshot =
            (await new V1LibraryReader()
                .ReadAsync(path))!;

        Assert.AreEqual(
            0,
            snapshot.Library.Favorites.Count);
        Assert.AreEqual(
            0,
            snapshot.Library.Recents.Count);
        Assert.AreEqual(
            2,
            snapshot.Warnings.Count);
        Assert.IsFalse(
            string.Join(
                    Environment.NewLine,
                    snapshot.Warnings)
                .Contains(
                    privateUrl,
                    StringComparison.Ordinal));
        Assert.AreEqual(
            json,
            await File.ReadAllTextAsync(path));
    }

    [TestMethod]
    public async Task ReadAsync_VersionedLibrary_ThrowsAndPreservesSource()
    {
        string path =
            Path.Combine(
                _testDirectory,
                "library.json");

        const string json =
            """
            {
              "schemaVersion": 1,
              "favorites": [],
              "recents": []
            }
            """;

        await File.WriteAllTextAsync(
            path,
            json);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => new V1LibraryReader()
                .ReadAsync(path));

        Assert.AreEqual(
            json,
            await File.ReadAllTextAsync(path));
    }
}
