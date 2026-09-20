using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Platform.Windows.Display;

namespace CopyGIF.Platform.Windows.Tests.Display;

[TestClass]
public sealed class WindowPlacementCalculatorTests
{
    [TestMethod]
    public void Calculate_MousePlacement_OffsetsFromPointer()
    {
        DisplayMonitor monitor =
            CreateMonitor(
                "DISPLAY1",
                new PhysicalRectangle(
                    0,
                    0,
                    1920,
                    1080));

        WindowPlacementResult result =
            Calculate(
                new WindowSettings
                {
                    PlacementMode =
                        WindowPlacementMode.Mouse
                },
                new[] { monitor },
                new PhysicalPoint(
                    100,
                    200));

        Assert.AreEqual(
            116,
            result.Left);

        Assert.AreEqual(
            216,
            result.Top);

        Assert.AreEqual(
            760,
            result.Width);

        Assert.AreEqual(
            560,
            result.Height);

        Assert.IsFalse(
            result.WasRecoveredOnScreen);
    }

    [TestMethod]
    public void Calculate_MouseNearBottomRight_ClampsToWorkArea()
    {
        DisplayMonitor monitor =
            CreateMonitor(
                "DISPLAY1",
                new PhysicalRectangle(
                    0,
                    0,
                    1920,
                    1080));

        WindowPlacementResult result =
            Calculate(
                new WindowSettings
                {
                    PlacementMode =
                        WindowPlacementMode.Mouse
                },
                new[] { monitor },
                new PhysicalPoint(
                    1900,
                    1050));

        Assert.AreEqual(
            1160,
            result.Left);

        Assert.AreEqual(
            520,
            result.Top);

        Assert.IsFalse(
            result.WasRecoveredOnScreen);
    }

    [TestMethod]
    public void Calculate_CenteredAt150Percent_ScalesAndCentersWindow()
    {
        DisplayMonitor monitor =
            CreateMonitor(
                "DISPLAY1",
                new PhysicalRectangle(
                    0,
                    0,
                    1920,
                    1080),
                dpi: 144);

        WindowPlacementResult result =
            Calculate(
                new WindowSettings
                {
                    PlacementMode =
                        WindowPlacementMode.Center
                },
                new[] { monitor },
                new PhysicalPoint(
                    500,
                    500));

        Assert.AreEqual(
            390,
            result.Left);

        Assert.AreEqual(
            120,
            result.Top);

        Assert.AreEqual(
            1140,
            result.Width);

        Assert.AreEqual(
            840,
            result.Height);

        Assert.IsFalse(
            result.WasRecoveredOnScreen);
    }

    [TestMethod]
    public void Calculate_RememberedPlacement_PreservesValidCoordinates()
    {
        DisplayMonitor monitor =
            CreateMonitor(
                "DISPLAY1",
                new PhysicalRectangle(
                    0,
                    0,
                    1920,
                    1080));

        WindowPlacementResult result =
            Calculate(
                new WindowSettings
                {
                    PlacementMode =
                        WindowPlacementMode.Remember,
                    Left = 100,
                    Top = 150,
                    LastMonitorId = "DISPLAY1"
                },
                new[] { monitor },
                new PhysicalPoint(
                    500,
                    500));

        Assert.AreEqual(
            100,
            result.Left);

        Assert.AreEqual(
            150,
            result.Top);

        Assert.AreEqual(
            "DISPLAY1",
            result.MonitorId);

        Assert.IsFalse(
            result.WasRecoveredOnScreen);
    }

    [TestMethod]
    public void Calculate_RememberedPlacementOffScreen_RecoversToWorkArea()
    {
        DisplayMonitor monitor =
            CreateMonitor(
                "DISPLAY1",
                new PhysicalRectangle(
                    0,
                    0,
                    1920,
                    1080));

        WindowPlacementResult result =
            Calculate(
                new WindowSettings
                {
                    PlacementMode =
                        WindowPlacementMode.Remember,
                    Left = 5000,
                    Top = 5000,
                    LastMonitorId = "DISPLAY1"
                },
                new[] { monitor },
                new PhysicalPoint(
                    500,
                    500));

        Assert.AreEqual(
            1160,
            result.Left);

        Assert.AreEqual(
            520,
            result.Top);

        Assert.IsTrue(
            result.WasRecoveredOnScreen);
    }

    [TestMethod]
    public void Calculate_RemovedMonitor_UsesNearestRemainingMonitor()
    {
        DisplayMonitor leftMonitor =
            CreateMonitor(
                "DISPLAY-LEFT",
                new PhysicalRectangle(
                    -1920,
                    0,
                    0,
                    1080));

        DisplayMonitor primaryMonitor =
            CreateMonitor(
                "DISPLAY-PRIMARY",
                new PhysicalRectangle(
                    0,
                    0,
                    1920,
                    1080),
                isPrimary: true);

        WindowPlacementResult result =
            Calculate(
                new WindowSettings
                {
                    PlacementMode =
                        WindowPlacementMode.Remember,
                    Left = -1500,
                    Top = 100,
                    LastMonitorId = "DISPLAY-REMOVED"
                },
                new[]
                {
                    leftMonitor,
                    primaryMonitor
                },
                new PhysicalPoint(
                    500,
                    500));

        Assert.AreEqual(
            "DISPLAY-LEFT",
            result.MonitorId);

        Assert.AreEqual(
            -1500,
            result.Left);

        Assert.AreEqual(
            100,
            result.Top);

        Assert.IsTrue(
            result.WasRecoveredOnScreen);
    }

    [TestMethod]
    public void Calculate_WindowLargerThanWorkArea_ShrinksToFit()
    {
        DisplayMonitor monitor =
            CreateMonitor(
                "DISPLAY1",
                new PhysicalRectangle(
                    0,
                    0,
                    800,
                    600),
                dpi: 192);

        WindowPlacementResult result =
            Calculate(
                new WindowSettings
                {
                    PlacementMode =
                        WindowPlacementMode.Center,
                    Width = 1800,
                    Height = 1400
                },
                new[] { monitor },
                new PhysicalPoint(
                    100,
                    100));

        Assert.AreEqual(
            0,
            result.Left);

        Assert.AreEqual(
            0,
            result.Top);

        Assert.AreEqual(
            800,
            result.Width);

        Assert.AreEqual(
            600,
            result.Height);

        Assert.IsTrue(
            result.WasRecoveredOnScreen);
    }

    [TestMethod]
    public void Calculate_RememberWithoutCoordinates_CentersAndMarksRecovered()
    {
        DisplayMonitor monitor =
            CreateMonitor(
                "DISPLAY1",
                new PhysicalRectangle(
                    0,
                    0,
                    1920,
                    1080));

        WindowPlacementResult result =
            Calculate(
                new WindowSettings
                {
                    PlacementMode =
                        WindowPlacementMode.Remember,
                    LastMonitorId = "DISPLAY1"
                },
                new[] { monitor },
                new PhysicalPoint(
                    100,
                    100));

        Assert.AreEqual(
            580,
            result.Left);

        Assert.AreEqual(
            260,
            result.Top);

        Assert.IsTrue(
            result.WasRecoveredOnScreen);
    }

    private static WindowPlacementResult Calculate(
        WindowSettings settings,
        IReadOnlyList<DisplayMonitor> monitors,
        PhysicalPoint cursorPosition)
    {
        return WindowPlacementCalculator.Calculate(
            settings,
            monitors,
            cursorPosition);
    }

    private static DisplayMonitor CreateMonitor(
        string id,
        PhysicalRectangle workArea,
        uint dpi = 96,
        bool isPrimary = false)
    {
        return new DisplayMonitor
        {
            Id = id,
            WorkArea = workArea,
            DpiX = dpi,
            DpiY = dpi,
            IsPrimary = isPrimary
        };
    }
}
