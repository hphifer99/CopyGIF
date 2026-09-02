namespace CopyGIF.Platform.Windows.Display;

internal interface IDisplayEnvironment
{
    IReadOnlyList<DisplayMonitor> GetMonitors();

    PhysicalPoint GetCursorPosition();
}
