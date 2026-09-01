using System.Text;
using CopyGIF.Infrastructure.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Infrastructure.Tests.Storage;

[TestClass]
public sealed class AtomicFileWriterTests
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
    public async Task WriteAsync_FirstWrite_CreatesDestination()
    {
        string destination =
            Path.Combine(
                _testDirectory,
                "settings.json");

        string backup =
            destination +
            ".bak";

        AtomicFileWriter writer =
            new();

        await WriteTextAsync(
            writer,
            destination,
            backup,
            "first");

        Assert.AreEqual(
            "first",
            await File.ReadAllTextAsync(
                destination));

        Assert.IsFalse(
            File.Exists(backup));
    }

    [TestMethod]
    public async Task WriteAsync_Replacement_PreservesPriorVersion()
    {
        string destination =
            Path.Combine(
                _testDirectory,
                "settings.json");

        string backup =
            destination +
            ".bak";

        AtomicFileWriter writer =
            new();

        await WriteTextAsync(
            writer,
            destination,
            backup,
            "first");

        await WriteTextAsync(
            writer,
            destination,
            backup,
            "second");

        Assert.AreEqual(
            "second",
            await File.ReadAllTextAsync(
                destination));

        Assert.AreEqual(
            "first",
            await File.ReadAllTextAsync(
                backup));
    }

    [TestMethod]
    public async Task WriteAsync_FailedWriter_LeavesDestinationUntouched()
    {
        string destination =
            Path.Combine(
                _testDirectory,
                "settings.json");

        string backup =
            destination +
            ".bak";

        await File.WriteAllTextAsync(
            destination,
            "original");

        AtomicFileWriter writer =
            new();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(
                destination,
                backup,
                async (stream, cancellationToken) =>
                {
                    byte[] bytes =
                        Encoding.UTF8.GetBytes(
                            "partial");

                    await stream.WriteAsync(
                        bytes,
                        cancellationToken);

                    throw new InvalidOperationException(
                        "Simulated write failure.");
                }));

        Assert.AreEqual(
            "original",
            await File.ReadAllTextAsync(
                destination));

        Assert.AreEqual(
            0,
            Directory.GetFiles(
                _testDirectory,
                "*.tmp").Length);
    }

    [TestMethod]
    public async Task WriteAsync_ConcurrentWrites_AreSerialized()
    {
        string destination =
            Path.Combine(
                _testDirectory,
                "settings.json");

        string backup =
            destination +
            ".bak";

        AtomicFileWriter writer =
            new();

        Task first =
            WriteTextAsync(
                writer,
                destination,
                backup,
                "first");

        Task second =
            WriteTextAsync(
                writer,
                destination,
                backup,
                "second");

        await Task.WhenAll(
            first,
            second);

        string current =
            await File.ReadAllTextAsync(
                destination);

        string previous =
            await File.ReadAllTextAsync(
                backup);

        CollectionAssert.AreEquivalent(
            new[]
            {
                "first",
                "second"
            },
            new[]
            {
                current,
                previous
            });

        Assert.AreEqual(
            0,
            Directory.GetFiles(
                _testDirectory,
                "*.tmp").Length);
    }

    private static Task WriteTextAsync(
        AtomicFileWriter writer,
        string destination,
        string backup,
        string value)
    {
        return writer.WriteAsync(
            destination,
            backup,
            async (stream, cancellationToken) =>
            {
                byte[] bytes =
                    Encoding.UTF8.GetBytes(value);

                await stream.WriteAsync(
                    bytes,
                    cancellationToken);
            });
    }
}
