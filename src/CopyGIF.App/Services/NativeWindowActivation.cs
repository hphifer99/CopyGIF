using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace CopyGIF.App.Services;

internal static class NativeWindowActivation
{
    public static bool IsProcessForeground()
    {
        nint foreground = GetForegroundWindow();
        if (foreground == 0) return false;
        if (GetWindowThreadProcessId(foreground, out uint processId) == 0) return false;
        return processId == (uint)Environment.ProcessId;
    }

    public static bool RestoreAndActivate(Window window)
    {
        nint handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        ShowWindow(handle, IsIconic(handle) ? 9 : 5);
        window.AppWindow.Show();
        window.Activate();
        bool requested = SetForegroundWindow(handle);
        bool foreground = GetForegroundWindow() == handle;
        if (!foreground)
        {
            CopyGIF.Core.Models.RepairDiagnostics.Record("activation", "windows", requested ? "foreground-not-granted" : "request-denied");
            var flash = new FlashInfo { Size = (uint)Marshal.SizeOf<FlashInfo>(), Window = handle,
                Flags = 3, Count = 2 };
            FlashWindowEx(ref flash);
        }
        return foreground;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public nint Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FlashWindowEx(ref FlashInfo info);
}
