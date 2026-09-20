using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CopyGIF.Core.Models;
using CopyGIF.Infrastructure.Storage;
using CopyGIF.Infrastructure.Tests.TestDoubles;
using CopyGIF.Infrastructure.Time;
using CopyGIF.Infrastructure.Updates;

namespace CopyGIF.Infrastructure.Tests.Updates;

[TestClass]
public sealed class UpdateInfrastructureTests
{
    private static readonly JsonSerializerOptions
        ManifestSerializerOptions =
            new(JsonSerializerDefaults.Web);

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
    public void ManifestParser_ValidManifest_ReturnsCompleteModel()
    {
        UpdateManifest expected =
            CreateManifest(
                Encoding.UTF8.GetBytes(
                    "signed-msi-content"));

        UpdateManifestParser parser =
            new();

        UpdateManifest result =
            parser.Parse(
                SerializeManifest(
                    expected),
                "stable");

        Assert.AreEqual(
            expected,
            result);
    }

    [TestMethod]
    public void ManifestParser_DuplicateProperty_RejectsManifest()
    {
        string json =
            """
            {
              "schemaVersion": 1,
              "version": "2.1.0",
              "version": "2.2.0"
            }
            """;

        UpdateManifestParser parser =
            new();

        Assert.ThrowsExactly<InvalidDataException>(
            () => parser.Parse(
                Encoding.UTF8.GetBytes(
                    json),
                "stable"));
    }

    [TestMethod]
    public void ManifestParser_AssetOutsideRepository_RejectsManifest()
    {
        UpdateManifest manifest =
            CreateManifest(
                [
                    1,
                    2,
                    3
                ]) with
            {
                AssetUri =
                    new Uri(
                        "https://example.com/CopyGIF-2.1.0-x64.msi")
            };

        UpdateManifestParser parser =
            new();

        Assert.ThrowsExactly<InvalidDataException>(
            () => parser.Parse(
                SerializeManifest(
                    manifest),
                "stable"));
    }

    [TestMethod]
    public async Task UpdateFeed_ValidManifest_UsesFixedLatestReleaseAddress()
    {
        UpdateManifest manifest =
            CreateManifest(
                Encoding.UTF8.GetBytes(
                    "signed-msi-content"));

        TestHttpMessageHandler handler =
            new(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new ByteArrayContent(
                                SerializeManifest(
                                    manifest))
                    });

        using HttpClient client =
            new(handler);

        GitHubUpdateFeed feed =
            new(
                client,
                new UpdateManifestParser());

        UpdateManifest? result =
            await feed.GetLatestAsync(
                "stable");

        Assert.AreEqual(
            manifest,
            result);

        Assert.AreEqual(
            new Uri(
                "https://github.com/hphifer99/CopyGIF/releases/latest/download/CopyGIF-update.json"),
            handler.LastRequestUri);
    }

    [TestMethod]
    public async Task UpdateFeed_MissingManifest_ReturnsNull()
    {
        TestHttpMessageHandler handler =
            new(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.NotFound));

        using HttpClient client =
            new(handler);

        GitHubUpdateFeed feed =
            new(
                client,
                new UpdateManifestParser());

        UpdateManifest? result =
            await feed.GetLatestAsync(
                "stable");

        Assert.IsNull(
            result);
    }

    [TestMethod]
    public async Task UpdateFeed_RedirectOutsideGitHub_RejectsDestination()
    {
        TestHttpMessageHandler handler =
            new(
                _ =>
                {
                    HttpResponseMessage response =
                        new(
                            HttpStatusCode.Redirect);

                    response.Headers.Location =
                        new Uri(
                            "https://example.com/update.json");

                    return response;
                });

        using HttpClient client =
            new(handler);

        GitHubUpdateFeed feed =
            new(
                client,
                new UpdateManifestParser());

        await Assert.ThrowsExactlyAsync<
            InvalidDataException>(
            () => feed.GetLatestAsync(
                "stable"));
    }

    [TestMethod]
    public async Task PackageService_ValidPackage_StreamsHashesAndStoresOwnedFile()
    {
        byte[] content =
            Encoding.UTF8.GetBytes(
                "signed-msi-content");

        UpdateManifest manifest =
            CreateManifest(
                content);

        TestHttpMessageHandler handler =
            new(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new ByteArrayContent(
                                content)
                    });

        using HttpClient client =
            new(handler);

        ApplicationPaths paths =
            new(_testDirectory);

        InlineProgress progress =
            new();

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        DownloadedUpdatePackage package =
            await service.DownloadAsync(
                manifest,
                progress);

        Assert.IsTrue(
            File.Exists(
                package.FilePath));

        Assert.AreEqual(
            Path.Combine(
                paths.UpdatesDirectory,
                manifest.AssetName),
            package.FilePath);

        Assert.AreEqual(
            content.LongLength,
            package.SizeBytes);

        Assert.AreEqual(
            manifest.Sha256,
            package.Sha256);

        Assert.AreEqual(
            content.LongLength,
            progress.Values[^1]
                .BytesReceived);
    }

    [TestMethod]
    public async Task PackageService_HashMismatch_RemovesPartialFile()
    {
        byte[] content =
            Encoding.UTF8.GetBytes(
                "tampered-msi-content");

        UpdateManifest manifest =
            CreateManifest(
                content) with
            {
                Sha256 =
                    new string(
                        '0',
                        64)
            };

        TestHttpMessageHandler handler =
            new(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new ByteArrayContent(
                                content)
                    });

        using HttpClient client =
            new(handler);

        ApplicationPaths paths =
            new(_testDirectory);

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        await Assert.ThrowsExactlyAsync<
            InvalidDataException>(
            () => service.DownloadAsync(
                manifest));

        Assert.HasCount(
            0,
            Directory.GetFiles(
                paths.UpdatesDirectory));
    }

    [TestMethod]
    public async Task PackageService_DeclaredLengthMismatch_RejectsBeforeWriting()
    {
        byte[] content =
            Encoding.UTF8.GetBytes(
                "short");

        UpdateManifest manifest =
            CreateManifest(
                Encoding.UTF8.GetBytes(
                    "different-content"));

        TestHttpMessageHandler handler =
            new(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new ByteArrayContent(
                                content)
                    });

        using HttpClient client =
            new(handler);

        ApplicationPaths paths =
            new(_testDirectory);

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        await Assert.ThrowsExactlyAsync<
            InvalidDataException>(
            () => service.DownloadAsync(
                manifest));

        Assert.HasCount(
            0,
            Directory.GetFiles(
                paths.UpdatesDirectory));
    }

    [TestMethod]
    public async Task PackageService_Delete_RemovesOwnedPackage()
    {
        byte[] content =
            Encoding.UTF8.GetBytes(
                "signed-msi-content");

        UpdateManifest manifest =
            CreateManifest(
                content);

        TestHttpMessageHandler handler =
            new(
                _ =>
                    new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new ByteArrayContent(
                                content)
                    });

        using HttpClient client =
            new(handler);

        ApplicationPaths paths =
            new(_testDirectory);

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        DownloadedUpdatePackage package =
            await service.DownloadAsync(
                manifest);

        await service.DeleteAsync(
            package);

        Assert.IsFalse(
            File.Exists(
                package.FilePath));
    }

    [TestMethod]
    public async Task PackageService_ExistingVerifiedPackage_IsReusedWithoutDownloading()
    {
        byte[] content =
            Encoding.UTF8.GetBytes(
                "signed-msi-content");

        UpdateManifest manifest =
            CreateManifest(
                content);

        int requestCount = 0;

        TestHttpMessageHandler handler =
            new(
                _ =>
                {
                    requestCount++;

                    return new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new ByteArrayContent(
                                content)
                    };
                });

        using HttpClient client =
            new(handler);

        ApplicationPaths paths =
            new(_testDirectory);

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        DownloadedUpdatePackage first =
            await service.DownloadAsync(
                manifest);

        DownloadedUpdatePackage second =
            await service.DownloadAsync(
                manifest);

        Assert.AreEqual(
            1,
            requestCount);

        Assert.AreEqual(
            first.FilePath,
            second.FilePath);

        Assert.AreEqual(
            manifest.Sha256,
            second.Sha256);

        Assert.AreEqual(
            content.LongLength,
            second.SizeBytes);
    }

    [TestMethod]
    public async Task PackageService_ExistingCorruptPackage_IsDownloadedAgain()
    {
        byte[] content =
            Encoding.UTF8.GetBytes(
                "signed-msi-content");

        UpdateManifest manifest =
            CreateManifest(
                content);

        int requestCount = 0;

        TestHttpMessageHandler handler =
            new(
                _ =>
                {
                    requestCount++;

                    return new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new ByteArrayContent(
                                content)
                    };
                });

        using HttpClient client =
            new(handler);

        ApplicationPaths paths =
            new(_testDirectory);

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        DownloadedUpdatePackage first =
            await service.DownloadAsync(
                manifest);

        // Same length, different bytes: only the hash check can catch this.
        byte[] corrupted =
            (byte[])content.Clone();

        corrupted[0] ^= 0xFF;

        await File.WriteAllBytesAsync(
            first.FilePath,
            corrupted);

        DownloadedUpdatePackage second =
            await service.DownloadAsync(
                manifest);

        Assert.AreEqual(
            2,
            requestCount);

        CollectionAssert.AreEqual(
            content,
            await File.ReadAllBytesAsync(
                second.FilePath));
    }

    [TestMethod]
    public async Task PackageService_Prune_RemovesOldInstallersAndKeepsNewerOnes()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        paths.EnsureDirectoriesExist();

        string oldInstaller =
            WriteUpdateFile(
                paths,
                "CopyGIF-1.9.0-win-x64.msi");

        string runningInstaller =
            WriteUpdateFile(
                paths,
                "CopyGIF-2.0.0-win-x64.msi");

        string pendingInstaller =
            WriteUpdateFile(
                paths,
                "CopyGIF-2.1.0-win-x64.msi");

        string unrelated =
            WriteUpdateFile(
                paths,
                "notes.txt");

        using HttpClient client =
            new(
                new TestHttpMessageHandler(
                    _ =>
                        new HttpResponseMessage(
                            HttpStatusCode.OK)));

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        await service.PruneAsync(
            "2.0.0");

        Assert.IsFalse(
            File.Exists(
                oldInstaller));

        Assert.IsFalse(
            File.Exists(
                runningInstaller));

        Assert.IsTrue(
            File.Exists(
                pendingInstaller));

        Assert.IsTrue(
            File.Exists(
                unrelated));
    }

    [TestMethod]
    public async Task PackageService_Prune_IgnoresPrereleaseSuffixOnTheRunningVersion()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        paths.EnsureDirectoriesExist();

        string runningInstaller =
            WriteUpdateFile(
                paths,
                "CopyGIF-2.0.0-win-x64.msi");

        using HttpClient client =
            new(
                new TestHttpMessageHandler(
                    _ =>
                        new HttpResponseMessage(
                            HttpStatusCode.OK)));

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        await service.PruneAsync(
            "2.0.0+build.7");

        Assert.IsFalse(
            File.Exists(
                runningInstaller));
    }

    [TestMethod]
    public async Task PackageService_Prune_RemovesOnlyStalePartialDownloads()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        paths.EnsureDirectoriesExist();

        string stale =
            WriteUpdateFile(
                paths,
                ".CopyGIF-2.1.0-win-x64.msi.aaaa.tmp");

        string fresh =
            WriteUpdateFile(
                paths,
                ".CopyGIF-2.1.0-win-x64.msi.bbbb.tmp");

        File.SetLastWriteTimeUtc(
            stale,
            DateTime.UtcNow.AddHours(-3));

        using HttpClient client =
            new(
                new TestHttpMessageHandler(
                    _ =>
                        new HttpResponseMessage(
                            HttpStatusCode.OK)));

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        await service.PruneAsync(
            "2.0.0");

        Assert.IsFalse(
            File.Exists(
                stale));

        Assert.IsTrue(
            File.Exists(
                fresh));
    }

    [TestMethod]
    public async Task PackageService_Prune_UnparseableVersion_DeletesNothing()
    {
        ApplicationPaths paths =
            new(_testDirectory);

        paths.EnsureDirectoriesExist();

        string installer =
            WriteUpdateFile(
                paths,
                "CopyGIF-1.0.0-win-x64.msi");

        using HttpClient client =
            new(
                new TestHttpMessageHandler(
                    _ =>
                        new HttpResponseMessage(
                            HttpStatusCode.OK)));

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        await service.PruneAsync(
            "not-a-version");

        Assert.IsTrue(
            File.Exists(
                installer));
    }

    [TestMethod]
    public async Task PackageService_FindExisting_VerifiedPackage_IsFoundWithoutTheNetwork()
    {
        byte[] content =
            Encoding.UTF8.GetBytes(
                "signed-msi-content");

        UpdateManifest manifest =
            CreateManifest(
                content);

        int requestCount = 0;

        TestHttpMessageHandler handler =
            new(
                _ =>
                {
                    requestCount++;

                    return new HttpResponseMessage(
                        HttpStatusCode.OK)
                    {
                        Content =
                            new ByteArrayContent(
                                content)
                    };
                });

        using HttpClient client =
            new(handler);

        ApplicationPaths paths =
            new(_testDirectory);

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        DownloadedUpdatePackage downloaded =
            await service.DownloadAsync(
                manifest);

        Assert.AreEqual(
            1,
            requestCount);

        DownloadedUpdatePackage? found =
            await service.FindExistingAsync(
                manifest);

        Assert.IsNotNull(
            found);

        Assert.AreEqual(
            downloaded.FilePath,
            found!.FilePath);

        Assert.AreEqual(
            manifest.Sha256,
            found.Sha256);

        Assert.AreEqual(
            manifest,
            found.Manifest);

        Assert.AreEqual(
            1,
            requestCount,
            "finding an existing package must not download anything");
    }

    [TestMethod]
    public async Task PackageService_FindExisting_NoFile_ReturnsNull()
    {
        byte[] content =
            Encoding.UTF8.GetBytes(
                "signed-msi-content");

        using HttpClient client =
            new(
                new TestHttpMessageHandler(
                    _ =>
                        throw new InvalidOperationException(
                            "No request may be made.")));

        ApplicationPaths paths =
            new(_testDirectory);

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        Assert.IsNull(
            await service.FindExistingAsync(
                CreateManifest(
                    content)));
    }

    [TestMethod]
    public async Task PackageService_FindExisting_ChangedFile_ReturnsNull()
    {
        byte[] content =
            Encoding.UTF8.GetBytes(
                "signed-msi-content");

        UpdateManifest manifest =
            CreateManifest(
                content);

        using HttpClient client =
            new(
                new TestHttpMessageHandler(
                    _ =>
                        new HttpResponseMessage(
                            HttpStatusCode.OK)
                        {
                            Content =
                                new ByteArrayContent(
                                    content)
                        }));

        ApplicationPaths paths =
            new(_testDirectory);

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        DownloadedUpdatePackage first =
            await service.DownloadAsync(
                manifest);

        // Same length, different bytes: only the hash check can catch this.
        byte[] changed =
            (byte[])content.Clone();

        changed[0] ^= 0xFF;

        await File.WriteAllBytesAsync(
            first.FilePath,
            changed);

        Assert.IsNull(
            await service.FindExistingAsync(
                manifest));
    }

    [TestMethod]
    public async Task PackageService_FindExisting_ManifestThatNoLongerValidates_ReturnsNull()
    {
        byte[] content =
            Encoding.UTF8.GetBytes(
                "signed-msi-content");

        UpdateManifest manifest =
            CreateManifest(
                content);

        using HttpClient client =
            new(
                new TestHttpMessageHandler(
                    _ =>
                        new HttpResponseMessage(
                            HttpStatusCode.OK)
                        {
                            Content =
                                new ByteArrayContent(
                                    content)
                        }));

        ApplicationPaths paths =
            new(_testDirectory);

        HttpUpdatePackageService service =
            CreatePackageService(
                client,
                paths);

        await service.DownloadAsync(
            manifest);

        // A stored manifest that points outside the updates folder must find nothing.
        UpdateManifest traversal =
            manifest with
            {
                AssetName =
                    "..\\..\\CopyGIF-2.1.0-x64.msi"
            };

        Assert.IsNull(
            await service.FindExistingAsync(
                traversal));

        UpdateManifest wrongChannel =
            manifest with
            {
                Channel = "beta"
            };

        Assert.IsNull(
            await service.FindExistingAsync(
                wrongChannel));
    }

    private static string WriteUpdateFile(
        ApplicationPaths paths,
        string fileName)
    {
        string path =
            Path.Combine(
                paths.UpdatesDirectory,
                fileName);

        File.WriteAllText(
            path,
            "content");

        return path;
    }

    private static HttpUpdatePackageService
        CreatePackageService(
            HttpClient client,
            ApplicationPaths paths)
    {
        return new HttpUpdatePackageService(
            client,
            paths,
            new OwnedPathGuard(),
            new SystemClock());
    }

    private static UpdateManifest CreateManifest(
        byte[] packageContent)
    {
        const string version = "2.1.0";
        const string assetName =
            "CopyGIF-2.1.0-x64.msi";

        return new UpdateManifest
        {
            Version = version,
            Channel = "stable",
            AssetName = assetName,
            AssetUri =
                new Uri(
                    $"https://github.com/hphifer99/CopyGIF/releases/download/v{version}/{assetName}"),
            SizeBytes = packageContent.LongLength,
            Sha256 =
                Convert.ToHexString(
                        SHA256.HashData(
                            packageContent))
                    .ToLowerInvariant(),
            MinimumSupportedVersion = "2.0.0",
            ReleaseNotesUri =
                new Uri(
                    $"https://github.com/hphifer99/CopyGIF/releases/tag/v{version}"),
            PublishedAtUtc =
                new DateTimeOffset(
                    2026,
                    9,
                    3,
                    12,
                    0,
                    0,
                    TimeSpan.Zero)
        };
    }

    private static byte[] SerializeManifest(
        UpdateManifest manifest)
    {
        return JsonSerializer.SerializeToUtf8Bytes(
            manifest,
            ManifestSerializerOptions);
    }

    private sealed class InlineProgress :
        IProgress<UpdateDownloadProgress>
    {
        private readonly List<UpdateDownloadProgress>
            _values = [];

        public IReadOnlyList<UpdateDownloadProgress> Values =>
            _values.ToArray();

        public void Report(
            UpdateDownloadProgress value)
        {
            _values.Add(
                value);
        }
    }
}
