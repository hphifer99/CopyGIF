using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace CopyGIF.Platform.Windows.Display;

internal sealed class WindowsDisplayEnvironment :
    IDisplayEnvironment
{
    private const uint PrimaryMonitorFlag = 0x00000001;
    private const uint DefaultDpi = 96;
    private const int EffectiveDpiType = 0;

    public IReadOnlyList<DisplayMonitor> GetMonitors()
    {
        List<DisplayMonitor> monitors = [];

        MonitorEnumerationCallback callback =
            (
                nint monitorHandle,
                nint deviceContext,
                ref NativeRectangle monitorRectangle,
                nint data) =>
            {
                monitors.Add(
                    CreateMonitor(
                        monitorHandle));

                return true;
            };

        bool succeeded =
            EnumDisplayMonitors(
                nint.Zero,
                nint.Zero,
                callback,
                nint.Zero);

        GC.KeepAlive(callback);

        if (!succeeded)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows could not enumerate the available displays.");
        }

        if (monitors.Count == 0)
        {
            throw new InvalidOperationException(
                "Windows did not report an available display.");
        }

        return monitors.AsReadOnly();
    }

    public PhysicalPoint GetCursorPosition()
    {
        if (!GetCursorPos(
                out NativePoint point))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows could not determine the pointer position.");
        }

        return new PhysicalPoint(
            point.X,
            point.Y);
    }

    private static DisplayMonitor CreateMonitor(
        nint monitorHandle)
    {
        NativeMonitorInfo information =
            new()
            {
                Size =
                    checked((uint)
                        Marshal.SizeOf<NativeMonitorInfo>()),
                DeviceName = string.Empty
            };

        if (!GetMonitorInfo(
                monitorHandle,
                ref information))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows could not read display information.");
        }

        GetMonitorDpi(
            monitorHandle,
            out uint dpiX,
            out uint dpiY);

        string monitorId =
            string.IsNullOrWhiteSpace(
                information.DeviceName)
                ? "Monitor-" +
                    monitorHandle
                        .ToInt64()
                        .ToString(
                            "X",
                            CultureInfo.InvariantCulture)
                : information.DeviceName;

        return new DisplayMonitor
        {
            Id = monitorId,
            WorkArea =
                new PhysicalRectangle(
                    information.WorkArea.Left,
                    information.WorkArea.Top,
                    information.WorkArea.Right,
                    information.WorkArea.Bottom),
            DpiX = dpiX,
            DpiY = dpiY,
            IsPrimary =
                (information.Flags &
                    PrimaryMonitorFlag) != 0
        };
    }

    private static void GetMonitorDpi(
        nint monitorHandle,
        out uint dpiX,
        out uint dpiY)
    {
        int result =
            GetDpiForMonitor(
                monitorHandle,
                EffectiveDpiType,
                out dpiX,
                out dpiY);

        if (result != 0 ||
            dpiX == 0 ||
            dpiY == 0)
        {
            dpiX = DefaultDpi;
            dpiY = DefaultDpi;
        }
    }

    private delegate bool MonitorEnumerationCallback(
        nint monitorHandle,
        nint deviceContext,
        ref NativeRectangle monitorRectangle,
        nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;

        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;

        public int Top;

        public int Right;

        public int Bottom;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct NativeMonitorInfo
    {
        public uint Size;

        public NativeRectangle MonitorArea;

        public NativeRectangle WorkArea;

        public uint Flags;

        [MarshalAs(
            UnmanagedType.ByValTStr,
            SizeConst = 32)]
        public string DeviceName;
    }

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRectangle,
        MonitorEnumerationCallback callback,
        nint data);

    [DllImport(
        "user32.dll",
        EntryPoint = "GetMonitorInfoW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(
        nint monitorHandle,
        ref NativeMonitorInfo monitorInformation);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(
        out NativePoint point);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        nint monitorHandle,
        int dpiType,
        out uint dpiX,
        out uint dpiY);
}
