using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Core.Contracts;

public interface IWindowPlacementService
{
    Task<WindowPlacementResult> CalculateAsync(
        WindowSettings settings,
        CancellationToken cancellationToken = default);
}
