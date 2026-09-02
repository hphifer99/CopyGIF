namespace CopyGIF.Platform.Windows.Display;

internal readonly record struct PhysicalPoint(
    int X,
    int Y);

internal readonly record struct PhysicalRectangle(
    int Left,
    int Top,
    int Right,
    int Bottom)
{
    public int Width =>
        checked(Right - Left);

    public int Height =>
        checked(Bottom - Top);

    public bool Contains(
        PhysicalPoint point)
    {
        return point.X >= Left &&
               point.X < Right &&
               point.Y >= Top &&
               point.Y < Bottom;
    }

    public double DistanceSquaredTo(
        PhysicalPoint point)
    {
        double horizontalDistance =
            point.X < Left
                ? Left - (double)point.X
                : point.X >= Right
                    ? point.X - (double)(Right - 1)
                    : 0;

        double verticalDistance =
            point.Y < Top
                ? Top - (double)point.Y
                : point.Y >= Bottom
                    ? point.Y - (double)(Bottom - 1)
                    : 0;

        return
            horizontalDistance * horizontalDistance +
            verticalDistance * verticalDistance;
    }
}

internal sealed record DisplayMonitor
{
    public required string Id { get; init; }

    public required PhysicalRectangle WorkArea { get; init; }

    public required uint DpiX { get; init; }

    public required uint DpiY { get; init; }

    public bool IsPrimary { get; init; }
}
