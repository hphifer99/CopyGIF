using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Platform.Windows.Display;

internal static class WindowPlacementCalculator
{
    private const double DefaultWidth = 760;
    private const double DefaultHeight = 560;
    private const double StandardDpi = 96;
    private const int PointerOffset = 16;

    public static WindowPlacementResult Calculate(
        WindowSettings settings,
        IReadOnlyList<DisplayMonitor> monitors,
        PhysicalPoint cursorPosition)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(monitors);

        if (monitors.Count == 0)
        {
            throw new ArgumentException(
                "At least one display is required.",
                nameof(monitors));
        }

        ValidateMonitors(monitors);

        DisplaySelection selection =
            SelectMonitor(
                settings,
                monitors,
                cursorPosition);

        DisplayMonitor monitor =
            selection.Monitor;

        double logicalWidth =
            NormalizeLogicalDimension(
                settings.Width,
                AppSettingsValidator.MinimumWindowWidth,
                AppSettingsValidator.MaximumWindowWidth,
                DefaultWidth);

        double logicalHeight =
            NormalizeLogicalDimension(
                settings.Height,
                AppSettingsValidator.MinimumWindowHeight,
                AppSettingsValidator.MaximumWindowHeight,
                DefaultHeight);

        double requestedWidth =
            Math.Round(
                logicalWidth *
                NormalizeDpi(monitor.DpiX) /
                StandardDpi,
                MidpointRounding.AwayFromZero);

        double requestedHeight =
            Math.Round(
                logicalHeight *
                NormalizeDpi(monitor.DpiY) /
                StandardDpi,
                MidpointRounding.AwayFromZero);

        double width =
            Math.Min(
                requestedWidth,
                monitor.WorkArea.Width);

        double height =
            Math.Min(
                requestedHeight,
                monitor.WorkArea.Height);

        bool recovered =
            selection.WasRecovered ||
            width != requestedWidth ||
            height != requestedHeight;

        double left;
        double top;

        switch (settings.PlacementMode)
        {
            case WindowPlacementMode.Center:
                left =
                    CenterHorizontally(
                        monitor.WorkArea,
                        width);

                top =
                    CenterVertically(
                        monitor.WorkArea,
                        height);

                break;

            case WindowPlacementMode.Remember:
                if (IsUsableCoordinate(settings.Left) &&
                    IsUsableCoordinate(settings.Top))
                {
                    left =
                        settings.Left!.Value;

                    top =
                        settings.Top!.Value;
                }
                else
                {
                    left =
                        CenterHorizontally(
                            monitor.WorkArea,
                            width);

                    top =
                        CenterVertically(
                            monitor.WorkArea,
                            height);

                    recovered = true;
                }

                break;

            default:
                left =
                    cursorPosition.X +
                    PointerOffset;

                top =
                    cursorPosition.Y +
                    PointerOffset;

                break;
        }

        double recoveredLeft =
            ClampHorizontal(
                left,
                width,
                monitor.WorkArea);

        double recoveredTop =
            ClampVertical(
                top,
                height,
                monitor.WorkArea);

        if (settings.PlacementMode ==
                WindowPlacementMode.Remember &&
            (recoveredLeft != left ||
             recoveredTop != top))
        {
            recovered = true;
        }

        return new WindowPlacementResult
        {
            Left = recoveredLeft,
            Top = recoveredTop,
            Width = width,
            Height = height,
            MonitorId = monitor.Id,
            WasRecoveredOnScreen = recovered
        };
    }

    private static DisplaySelection SelectMonitor(
        WindowSettings settings,
        IReadOnlyList<DisplayMonitor> monitors,
        PhysicalPoint cursorPosition)
    {
        if (settings.PlacementMode ==
            WindowPlacementMode.Remember)
        {
            if (!string.IsNullOrWhiteSpace(
                    settings.LastMonitorId))
            {
                DisplayMonitor? rememberedMonitor =
                    monitors.FirstOrDefault(
                        monitor =>
                            string.Equals(
                                monitor.Id,
                                settings.LastMonitorId,
                                StringComparison.OrdinalIgnoreCase));

                if (rememberedMonitor is not null)
                {
                    return new DisplaySelection(
                        rememberedMonitor,
                        WasRecovered: false);
                }
            }

            if (IsUsableCoordinate(settings.Left) &&
                IsUsableCoordinate(settings.Top))
            {
                PhysicalPoint rememberedPoint =
                    new(
                        ClampToInteger(
                            settings.Left!.Value),
                        ClampToInteger(
                            settings.Top!.Value));

                return new DisplaySelection(
                    FindNearestMonitor(
                        monitors,
                        rememberedPoint),
                    WasRecovered: true);
            }

            return new DisplaySelection(
                FindNearestMonitor(
                    monitors,
                    cursorPosition),
                WasRecovered: true);
        }

        return new DisplaySelection(
            FindNearestMonitor(
                monitors,
                cursorPosition),
            WasRecovered: false);
    }

    private static DisplayMonitor FindNearestMonitor(
        IReadOnlyList<DisplayMonitor> monitors,
        PhysicalPoint point)
    {
        DisplayMonitor? containingMonitor =
            monitors.FirstOrDefault(
                monitor =>
                    monitor.WorkArea.Contains(point));

        if (containingMonitor is not null)
        {
            return containingMonitor;
        }

        return monitors
            .OrderBy(
                monitor =>
                    monitor.WorkArea
                        .DistanceSquaredTo(point))
            .ThenByDescending(
                monitor =>
                    monitor.IsPrimary)
            .First();
    }

    private static void ValidateMonitors(
        IReadOnlyList<DisplayMonitor> monitors)
    {
        foreach (DisplayMonitor monitor in monitors)
        {
            if (string.IsNullOrWhiteSpace(
                    monitor.Id))
            {
                throw new ArgumentException(
                    "Every display must have an identifier.",
                    nameof(monitors));
            }

            if (monitor.WorkArea.Width <= 0 ||
                monitor.WorkArea.Height <= 0)
            {
                throw new ArgumentException(
                    "Every display must have a usable work area.",
                    nameof(monitors));
            }
        }
    }

    private static double NormalizeLogicalDimension(
        double value,
        double minimum,
        double maximum,
        double fallback)
    {
        return double.IsFinite(value) &&
               value >= minimum &&
               value <= maximum
            ? value
            : fallback;
    }

    private static double NormalizeDpi(
        uint dpi)
    {
        return dpi == 0
            ? StandardDpi
            : dpi;
    }

    private static bool IsUsableCoordinate(
        double? coordinate)
    {
        return coordinate.HasValue &&
               double.IsFinite(coordinate.Value) &&
               Math.Abs(coordinate.Value) <=
                   AppSettingsValidator.MaximumAbsoluteCoordinate;
    }

    private static int ClampToInteger(
        double value)
    {
        if (value <= int.MinValue)
        {
            return int.MinValue;
        }

        if (value >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return checked((int)Math.Round(
            value,
            MidpointRounding.AwayFromZero));
    }

    private static double CenterHorizontally(
        PhysicalRectangle workArea,
        double width)
    {
        return workArea.Left +
               (workArea.Width - width) / 2;
    }

    private static double CenterVertically(
        PhysicalRectangle workArea,
        double height)
    {
        return workArea.Top +
               (workArea.Height - height) / 2;
    }

    private static double ClampHorizontal(
        double left,
        double width,
        PhysicalRectangle workArea)
    {
        return Math.Clamp(
            left,
            workArea.Left,
            workArea.Right - width);
    }

    private static double ClampVertical(
        double top,
        double height,
        PhysicalRectangle workArea)
    {
        return Math.Clamp(
            top,
            workArea.Top,
            workArea.Bottom - height);
    }

    private sealed record DisplaySelection(
        DisplayMonitor Monitor,
        bool WasRecovered);
}
