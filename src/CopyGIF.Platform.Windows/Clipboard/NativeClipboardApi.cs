using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CopyGIF.Platform.Windows.Clipboard;

internal interface IClipboardNativeApi
{
    bool TrySetFileDrop(
        nint ownerWindowHandle,
        byte[] payload,
        out int errorCode);
}

internal sealed class NativeClipboardApi :
    IClipboardNativeApi
{
    private const uint ClipboardFormatFileDrop = 15;
    private const uint GlobalMemoryFlags = 0x0042;

    public static NativeClipboardApi Instance { get; } =
        new();

    private NativeClipboardApi()
    {
    }

    public bool TrySetFileDrop(
        nint ownerWindowHandle,
        byte[] payload,
        out int errorCode)
    {
        ArgumentNullException.ThrowIfNull(payload);

        nint globalMemory =
            GlobalAlloc(
                GlobalMemoryFlags,
                checked((nuint)payload.Length));

        if (globalMemory == nint.Zero)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows could not allocate clipboard memory.");
        }

        bool clipboardOpened = false;

        try
        {
            nint destination =
                GlobalLock(globalMemory);

            if (destination == nint.Zero)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Windows could not lock clipboard memory.");
            }

            try
            {
                Marshal.Copy(
                    payload,
                    0,
                    destination,
                    payload.Length);
            }
            finally
            {
                _ = GlobalUnlock(globalMemory);
            }

            clipboardOpened =
                OpenClipboard(ownerWindowHandle);

            if (!clipboardOpened)
            {
                errorCode =
                    Marshal.GetLastWin32Error();

                return false;
            }

            if (!EmptyClipboard())
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Windows could not clear the clipboard.");
            }

            nint clipboardData =
                SetClipboardData(
                    ClipboardFormatFileDrop,
                    globalMemory);

            if (clipboardData == nint.Zero)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Windows rejected the GIF clipboard data.");
            }

            globalMemory = nint.Zero;
            errorCode = 0;

            return true;
        }
        finally
        {
            if (clipboardOpened)
            {
                _ = CloseClipboard();
            }

            if (globalMemory != nint.Zero)
            {
                _ = GlobalFree(globalMemory);
            }
        }
    }

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    private static extern nint GlobalAlloc(
        uint flags,
        nuint bytes);

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    private static extern nint GlobalLock(
        nint globalMemory);

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(
        nint globalMemory);

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    private static extern nint GlobalFree(
        nint globalMemory);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(
        nint newOwnerWindowHandle);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern nint SetClipboardData(
        uint format,
        nint memoryHandle);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();
}
