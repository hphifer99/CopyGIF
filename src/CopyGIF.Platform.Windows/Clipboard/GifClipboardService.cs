using System.ComponentModel;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Platform.Windows.Shell;

namespace CopyGIF.Platform.Windows.Clipboard;

public sealed class GifClipboardService :
    IClipboardService
{
    private const int ClipboardAttempts = 8;

    private static readonly byte[] Gif87a =
        "GIF87a"u8.ToArray();

    private static readonly byte[] Gif89a =
        "GIF89a"u8.ToArray();

    private readonly IWindowHandleProvider
        _windowHandleProvider;

    private readonly IClipboardNativeApi
        _nativeApi;

    public GifClipboardService(
        IWindowHandleProvider windowHandleProvider)
        : this(
            windowHandleProvider,
            NativeClipboardApi.Instance)
    {
    }

    internal GifClipboardService(
        IWindowHandleProvider windowHandleProvider,
        IClipboardNativeApi nativeApi)
    {
        _windowHandleProvider =
            windowHandleProvider ??
            throw new ArgumentNullException(
                nameof(windowHandleProvider));

        _nativeApi =
            nativeApi ??
            throw new ArgumentNullException(
                nameof(nativeApi));
    }

    public async Task CopyGifAsync(
        DownloadedGif gif,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gif);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPath =
            Path.GetFullPath(gif.FilePath);

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".gif",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Only a .gif file can be copied as a GIF.");
        }

        FileInfo file =
            new(fullPath);

        if (!file.Exists)
        {
            throw new FileNotFoundException(
                "The downloaded GIF file no longer exists.",
                fullPath);
        }

        if (file.Length != gif.SizeBytes)
        {
            throw new InvalidDataException(
                "The downloaded GIF file size changed before it could be copied.");
        }

        await VerifyGifSignatureAsync(
                fullPath,
                cancellationToken)
            .ConfigureAwait(false);

        byte[] payload =
            GifClipboardPayload.Create(fullPath);

        nint ownerWindowHandle =
            _windowHandleProvider
                .GetWindowHandle();

        if (ownerWindowHandle == nint.Zero)
        {
            throw new InvalidOperationException(
                "The application window must exist before a GIF can be copied.");
        }

        int lastError = 0;

        for (int attempt = 0;
             attempt < ClipboardAttempts;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_nativeApi.TrySetFileDrop(
                    ownerWindowHandle,
                    payload,
                    out lastError))
            {
                return;
            }

            if (attempt < ClipboardAttempts - 1)
            {
                await Task.Delay(
                        TimeSpan.FromMilliseconds(
                            25 * (attempt + 1)),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new Win32Exception(
            lastError,
            "The Windows clipboard is busy. Try copying the GIF again.");
    }

    private static async Task VerifyGifSignatureAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        byte[] signature =
            new byte[6];

        await using FileStream stream =
            new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

        int bytesRead =
            await stream.ReadAsync(
                    signature,
                    cancellationToken)
                .ConfigureAwait(false);

        bool isGif =
            bytesRead == signature.Length &&
            (signature.AsSpan()
                    .SequenceEqual(Gif87a) ||
                signature.AsSpan()
                    .SequenceEqual(Gif89a));

        if (!isGif)
        {
            throw new InvalidDataException(
                "The clipboard file does not contain a valid GIF signature.");
        }
    }
}
