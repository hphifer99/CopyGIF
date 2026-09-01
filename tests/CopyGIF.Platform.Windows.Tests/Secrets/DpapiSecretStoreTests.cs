using System.Text;
using CopyGIF.Platform.Windows.Secrets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Platform.Windows.Tests.Secrets;

[TestClass]
public sealed class DpapiSecretStoreTests
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
            if (Directory.Exists(_testDirectory))
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
    public async Task SetThenGetAsync_RoundTripsSecret()
    {
        DpapiSecretStore store =
            new(_testDirectory);

        string expected =
            "test-" +
            Guid.NewGuid().ToString("N");

        await store.SetAsync(
            "test.secret",
            expected);

        string? actual =
            await store.GetAsync(
                "test.secret");

        Assert.AreEqual(
            expected,
            actual);
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesSecret()
    {
        DpapiSecretStore store =
            new(_testDirectory);

        await store.SetAsync(
            "test.secret",
            "temporary-test-value");

        await store.DeleteAsync(
            "test.secret");

        string? actual =
            await store.GetAsync(
                "test.secret");

        Assert.IsNull(actual);
    }

    [TestMethod]
    public async Task SetAsync_DoesNotStorePlaintext()
    {
        DpapiSecretStore store =
            new(_testDirectory);

        string secret =
            "plaintext-check-" +
            Guid.NewGuid().ToString("N");

        await store.SetAsync(
            "test.secret",
            secret);

        string[] files =
            Directory.GetFiles(
                _testDirectory,
                "*.bin");

        Assert.AreEqual(
            1,
            files.Length);

        byte[] fileBytes =
            await File.ReadAllBytesAsync(
                files[0]);

        byte[] plainBytes =
            Encoding.UTF8.GetBytes(secret);

        Assert.IsFalse(
            ContainsSequence(
                fileBytes,
                plainBytes));
    }

    [TestMethod]
    public async Task DifferentNames_StoreIndependentSecrets()
    {
        DpapiSecretStore store =
            new(_testDirectory);

        await store.SetAsync(
            "first.secret",
            "first-value");

        await store.SetAsync(
            "second.secret",
            "second-value");

        string? first =
            await store.GetAsync(
                "first.secret");

        string? second =
            await store.GetAsync(
                "second.secret");

        Assert.AreEqual(
            "first-value",
            first);

        Assert.AreEqual(
            "second-value",
            second);

        Assert.AreNotEqual(
            first,
            second);
    }

    private static bool ContainsSequence(
        byte[] source,
        byte[] sequence)
    {
        if (sequence.Length == 0 ||
            source.Length < sequence.Length)
        {
            return false;
        }

        for (int i = 0;
             i <= source.Length - sequence.Length;
             i++)
        {
            bool match = true;

            for (int j = 0;
                 j < sequence.Length;
                 j++)
            {
                if (source[i + j] ==
                    sequence[j])
                {
                    continue;
                }

                match = false;
                break;
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }
}