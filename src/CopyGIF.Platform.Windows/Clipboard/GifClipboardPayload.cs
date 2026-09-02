using System.Buffers.Binary;
using System.Text;

namespace CopyGIF.Platform.Windows.Clipboard;

internal static class GifClipboardPayload
{
    private const int DropFilesHeaderSize = 20;

    public static byte[] Create(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string fullPath = Path.GetFullPath(filePath);

        if (fullPath.Contains('\0'))
        {
            throw new ArgumentException(
                "The clipboard file path cannot contain a null character.",
                nameof(filePath));
        }

        byte[] pathBytes =
            Encoding.Unicode.GetBytes(
                fullPath + "\0\0");

        byte[] payload =
            new byte[
                DropFilesHeaderSize +
                pathBytes.Length];

        BinaryPrimitives.WriteUInt32LittleEndian(
            payload.AsSpan(0, 4),
            DropFilesHeaderSize);

        BinaryPrimitives.WriteInt32LittleEndian(
            payload.AsSpan(16, 4),
            1);

        pathBytes.CopyTo(
            payload,
            DropFilesHeaderSize);

        return payload;
    }
}
