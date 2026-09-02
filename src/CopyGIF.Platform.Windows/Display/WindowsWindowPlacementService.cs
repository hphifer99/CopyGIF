using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Platform.Windows.Display;

public sealed class WindowsWindowPlacementService :
    IWindowPlacementService
{
    private readonly IDisplayEnvironment
        _displayEnvironment;

    public WindowsWindowPlacementService()
        : this(
            new WindowsDisplayEnvironment())
    {
    }

    internal WindowsWindowPlacementService(
        IDisplayEnvironment displayEnvironment)
    {
        _displayEnvironment =
            displayEnvironment ??
            throw new ArgumentNullException(
                nameof(displayEnvironment));
    }

    public Task<WindowPlacementResult> CalculateAsync(
        WindowSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<DisplayMonitor> monitors =
            _displayEnvironment.GetMonitors();

        cancellationToken.ThrowIfCancellationRequested();

        PhysicalPoint cursorPosition =
            _displayEnvironment.GetCursorPosition();

        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            WindowPlacementCalculator.Calculate(
                settings,
                monitors,
                cursorPosition));
    }
}
