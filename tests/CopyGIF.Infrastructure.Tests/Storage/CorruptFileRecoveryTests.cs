using System.Globalization;
using CopyGIF.Core.Policies;
using CopyGIF.Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Infrastructure.Tests.Storage;

[TestClass]
public sealed class CorruptFileRecoveryTests
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
    public async Task Preserve_MovesOriginalBytesToTimestampedFile()
    {
        string path =
            Path.Combine(
                _testDirectory,
                "settings.json");

        const string corruptContent =
            "{ definitely not json";

        await File.WriteAllTextAsync(
            path,
            corruptContent);

        CorruptFileRecovery recovery =
            new();

        string? preservedPath =
            recovery.Preserve(path);

        Assert.IsNotNull(
            preservedPath);

        Assert.IsFalse(
            File.Exists(path));

        Assert.IsTrue(
            File.Exists(preservedPath));

        StringAssert.StartsWith(
            Path.GetFileName(preservedPath),
            "settings.json.corrupt.");

        Assert.AreEqual(
            corruptContent,
            await File.ReadAllTextAsync(
                preservedPath));
    }

    [TestMethod]
    public void Preserve_MissingFile_ReturnsNull()
    {
        string path =
            Path.Combine(
                _testDirectory,
                "missing.json");

        CorruptFileRecovery recovery =
            new();

        Assert.IsNull(
            recovery.Preserve(path));
    }

    [TestMethod]
    public async Task Preserve_PrunesFilesBeyondPolicyLimit()
    {
        string path =
            Path.Combine(
                _testDirectory,
                "settings.json");

        CorruptFileRecovery recovery =
            new();

        int preservationCount =
            StoragePolicy.MaximumPreservedCorruptFiles +
            3;

        for (int index = 0;
             index < preservationCount;
             index++)
        {
            await File.WriteAllTextAsync(
                path,
                index.ToString(
                    CultureInfo.InvariantCulture));

            recovery.Preserve(path);
        }

        Assert.AreEqual(
            StoragePolicy.MaximumPreservedCorruptFiles,
            Directory.GetFiles(
                _testDirectory,
                "settings.json.corrupt.*").Length);
    }
}
