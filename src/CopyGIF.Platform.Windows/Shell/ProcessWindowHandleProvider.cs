using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CopyGIF.Platform.Windows.Shell;

public sealed class ProcessWindowHandleProvider :
    IWindowHandleProvider
{
    private const uint OwnerWindow = 4;

    public nint GetWindowHandle()
    {
        using Process process =
            Process.GetCurrentProcess();

        process.Refresh();

        if (process.MainWindowHandle !=
            nint.Zero)
        {
            return process.MainWindowHandle;
        }

        nint windowHandle = nint.Zero;
        uint processId =
            (uint)Environment.ProcessId;

        _ = EnumWindows(
            (candidate, parameter) =>
            {
                _ = GetWindowThreadProcessId(
                    candidate,
                    out uint candidateProcessId);

                if (candidateProcessId == processId &&
                    IsWindowVisible(candidate) &&
                    GetWindow(
                        candidate,
                        OwnerWindow) == nint.Zero)
                {
                    windowHandle = candidate;
                    return false;
                }

                return true;
            },
            nint.Zero);

        return windowHandle;
    }

    private delegate bool EnumWindowsCallback(
        nint windowHandle,
        nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(
        EnumWindowsCallback callback,
        nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(
        nint windowHandle);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(
        nint windowHandle,
        uint command);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        nint windowHandle,
        out uint processId);
}
