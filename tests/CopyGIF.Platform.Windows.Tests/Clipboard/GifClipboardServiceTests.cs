using System.Buffers.Binary;
using System.Text;
using CopyGIF.Core.Models;
using CopyGIF.Platform.Windows.Clipboard;
using CopyGIF.Platform.Windows.Shell;

namespace CopyGIF.Platform.Windows.Tests.Clipboard;

[TestClass]
public sealed class GifClipboardServiceTests
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
    public async Task CopyGifAsync_ValidGif_PublishesFileDropPayload()
    {
        string filePath =
            Path.Combine(
                _testDirectory,
                "valid.gif");

        byte[] fileBytes =
            "GIF89a-test-payload"u8.ToArray();

        await File.WriteAllBytesAsync(
            filePath,
            fileBytes);

        RecordingClipboardNativeApi nativeApi =
            new();

        GifClipboardService service =
            new(
                new StubWindowHandleProvider(
                    (nint)123),
                nativeApi);

        await service.CopyGifAsync(
            CreateDownloadedGif(
                filePath,
                fileBytes.Length));

        Assert.AreEqual(
            (nint)123,
            nativeApi.OwnerWindowHandle);

        Assert.IsNotNull(
            nativeApi.Payload);

        uint pathOffset =
            BinaryPrimitives.ReadUInt32LittleEndian(
                nativeApi.Payload.AsSpan(0, 4));

        Assert.AreEqual(
            20U,
            pathOffset);

        int isUnicode =
            BinaryPrimitives.ReadInt32LittleEndian(
                nativeApi.Payload.AsSpan(16, 4));

        Assert.AreEqual(
            1,
            isUnicode);

        string pathList =
            Encoding.Unicode.GetString(
                nativeApi.Payload,
                checked((int)pathOffset),
                nativeApi.Payload.Length -
                checked((int)pathOffset));

        Assert.IsTrue(
            pathList.StartsWith(
                Path.GetFullPath(filePath),
                StringComparison.Ordinal));

        Assert.IsTrue(
            pathList.EndsWith(
                "\0\0",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CopyGifAsync_InvalidSignature_DoesNotChangeClipboard()
    {
        string filePath =
            Path.Combine(
                _testDirectory,
                "invalid.gif");

        byte[] fileBytes =
            "not-a-gif"u8.ToArray();

        await File.WriteAllBytesAsync(
            filePath,
            fileBytes);

        RecordingClipboardNativeApi nativeApi =
            new();

        GifClipboardService service =
            new(
                new StubWindowHandleProvider(
                    (nint)123),
                nativeApi);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () =>
                service.CopyGifAsync(
                    CreateDownloadedGif(
                        filePath,
                        fileBytes.Length)));

        Assert.AreEqual(
            0,
            nativeApi.CallCount);
    }

    [TestMethod]
    public async Task CopyGifAsync_SizeChanged_DoesNotChangeClipboard()
    {
        string filePath =
            Path.Combine(
                _testDirectory,
                "changed.gif");

        byte[] fileBytes =
            "GIF89a-size"u8.ToArray();

        await File.WriteAllBytesAsync(
            filePath,
            fileBytes);

        RecordingClipboardNativeApi nativeApi =
            new();

        GifClipboardService service =
            new(
                new StubWindowHandleProvider(
                    nint.Zero),
                nativeApi);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () =>
                service.CopyGifAsync(
                    CreateDownloadedGif(
                        filePath,
                        fileBytes.Length + 1)));

        Assert.AreEqual(
            0,
            nativeApi.CallCount);
    }

    [TestMethod]
    public async Task CopyGifAsync_BusyClipboard_RetriesUntilSuccessful()
    {
        string filePath =
            Path.Combine(
                _testDirectory,
                "retry.gif");

        byte[] fileBytes =
            "GIF87a-retry"u8.ToArray();

        await File.WriteAllBytesAsync(
            filePath,
            fileBytes);

        RecordingClipboardNativeApi nativeApi =
            new(failuresBeforeSuccess: 2);

        GifClipboardService service =
            new(
                new StubWindowHandleProvider(
                    (nint)123),
                nativeApi);

        await service.CopyGifAsync(
            CreateDownloadedGif(
                filePath,
                fileBytes.Length));

        Assert.AreEqual(
            3,
            nativeApi.CallCount);
    }

    private static DownloadedGif CreateDownloadedGif(
        string filePath,
        long sizeBytes)
    {
        return new DownloadedGif
        {
            Identity =
                new GifIdentity(
                    "klipy",
                    "test-gif"),
            SourceUri =
                new Uri(
                    "https://media.example.com/test.gif"),
            FilePath = filePath,
            SizeBytes = sizeBytes,
            Sha256 = "TEST-HASH",
            DownloadedAtUtc =
                DateTimeOffset.UtcNow,
            Purpose =
                GifDownloadPurpose.Clipboard
        };
    }

    private sealed class StubWindowHandleProvider :
        IWindowHandleProvider
    {
        private readonly nint _handle;

        public StubWindowHandleProvider(
            nint handle)
        {
            _handle = handle;
        }

        public nint GetWindowHandle()
        {
            return _handle;
        }
    }

    private sealed class RecordingClipboardNativeApi :
        IClipboardNativeApi
    {
        private readonly int
            _failuresBeforeSuccess;

        public RecordingClipboardNativeApi(
            int failuresBeforeSuccess = 0)
        {
            _failuresBeforeSuccess =
                failuresBeforeSuccess;
        }

        public int CallCount { get; private set; }

        public nint OwnerWindowHandle { get; private set; }

        public byte[]? Payload { get; private set; }

        public bool TrySetFileDrop(
            nint ownerWindowHandle,
            byte[] payload,
            out int errorCode)
        {
            CallCount++;
            OwnerWindowHandle =
                ownerWindowHandle;
            Payload = payload;

            bool succeeded =
                CallCount >
                _failuresBeforeSuccess;

            errorCode =
                succeeded
                    ? 0
                    : 5;

            return succeeded;
        }
    }
}
