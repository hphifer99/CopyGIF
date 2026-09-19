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
    private const int DropEffectCopy = 1;

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
            // This is an advisory Shell format. CF_HDROP remains the payload
            // even when a target does not use the preferred action.
            TrySetPreferredCopyEffect();
            // Read the published format through the Shell parser, not our encoder.
            nint published = GetClipboardData(ClipboardFormatFileDrop);
            string expectedPath = System.Text.Encoding.Unicode.GetString(payload, 20, payload.Length - 20).TrimEnd('\0');
            if (published == nint.Zero || DragQueryFile(published, uint.MaxValue, null, 0) != 1)
                throw new Win32Exception(13, "Windows could not verify the copied GIF file list.");
            uint length = DragQueryFile(published, 0, null, 0);
            var actualPath = new char[checked((int)length + 1)];
            if (DragQueryFile(published, 0, actualPath, (uint)actualPath.Length) == 0 ||
                !string.Equals(expectedPath, new string(actualPath, 0, checked((int)length)), StringComparison.OrdinalIgnoreCase))
                throw new Win32Exception(13, "Windows could not verify the copied GIF file list.");

            if (!CloseClipboard())
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not release the clipboard.");
            clipboardOpened = false;
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

    private static void TrySetPreferredCopyEffect()
    {
        uint format = RegisterClipboardFormat("Preferred DropEffect");
        if (format == 0) return;
        nint memory = GlobalAlloc(GlobalMemoryFlags, (nuint)sizeof(int));
        if (memory == nint.Zero) return;
        try
        {
            nint pointer = GlobalLock(memory);
            if (pointer == nint.Zero) return;
            try { Marshal.WriteInt32(pointer, DropEffectCopy); }
            finally { _ = GlobalUnlock(memory); }
            if (SetClipboardData(format, memory) != nint.Zero) memory = nint.Zero;
        }
        finally { if (memory != nint.Zero) _ = GlobalFree(memory); }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetClipboardData(uint format);

    [DllImport("shell32.dll", EntryPoint = "DragQueryFileW", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(nint drop, uint index, [Out] char[]? fileName, uint size);

    [DllImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string format);

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
