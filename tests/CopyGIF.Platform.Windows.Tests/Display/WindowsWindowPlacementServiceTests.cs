using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Platform.Windows.Display;

namespace CopyGIF.Platform.Windows.Tests.Display;

[TestClass]
public sealed class WindowsWindowPlacementServiceTests
{
    [TestMethod]
    public async Task CalculateAsync_UsesCurrentDisplaySnapshot()
    {
        FakeDisplayEnvironment environment =
            new(
                new[]
                {
                    new DisplayMonitor
                    {
                        Id = "DISPLAY1",
                        WorkArea =
                            new PhysicalRectangle(
                                0,
                                0,
                                1920,
                                1080),
                        DpiX = 96,
                        DpiY = 96,
                        IsPrimary = true
                    }
                },
                new PhysicalPoint(
                    100,
                    100));

        WindowsWindowPlacementService service =
            new(environment);

        WindowPlacementResult result =
            await service.CalculateAsync(
                new WindowSettings
                {
                    PlacementMode =
                        WindowPlacementMode.Mouse
                });

        Assert.AreEqual(
            116,
            result.Left);

        Assert.AreEqual(
            116,
            result.Top);

        Assert.AreEqual(
            "DISPLAY1",
            result.MonitorId);

        Assert.AreEqual(
            1,
            environment.MonitorReadCount);

        Assert.AreEqual(
            1,
            environment.CursorReadCount);
    }

    [TestMethod]
    public async Task CalculateAsync_CanceledToken_DoesNotReadDisplays()
    {
        FakeDisplayEnvironment environment =
            new(
                Array.Empty<DisplayMonitor>(),
                new PhysicalPoint());

        WindowsWindowPlacementService service =
            new(environment);

        using CancellationTokenSource source =
            new();

        source.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () =>
                service.CalculateAsync(
                    new WindowSettings(),
                    source.Token));

        Assert.AreEqual(
            0,
            environment.MonitorReadCount);

        Assert.AreEqual(
            0,
            environment.CursorReadCount);
    }

    private sealed class FakeDisplayEnvironment :
        IDisplayEnvironment
    {
        private readonly IReadOnlyList<DisplayMonitor>
            _monitors;

        private readonly PhysicalPoint
            _cursorPosition;

        public FakeDisplayEnvironment(
            IReadOnlyList<DisplayMonitor> monitors,
            PhysicalPoint cursorPosition)
        {
            _monitors = monitors;
            _cursorPosition = cursorPosition;
        }

        public int MonitorReadCount { get; private set; }

        public int CursorReadCount { get; private set; }

        public IReadOnlyList<DisplayMonitor> GetMonitors()
        {
            MonitorReadCount++;

            return _monitors;
        }

        public PhysicalPoint GetCursorPosition()
        {
            CursorReadCount++;

            return _cursorPosition;
        }
    }
}
