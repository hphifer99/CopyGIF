namespace CopyGIF.Core.Models;

public sealed record WindowPlacementResult
{
    public required double Left { get; init; }

    public required double Top { get; init; }

    public required double Width { get; init; }

    public required double Height { get; init; }

    public string? MonitorId { get; init; }

    public bool WasRecoveredOnScreen { get; init; }
}
