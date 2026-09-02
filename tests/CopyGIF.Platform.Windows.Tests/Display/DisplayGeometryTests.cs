using CopyGIF.Platform.Windows.Display;

namespace CopyGIF.Platform.Windows.Tests.Display;

[TestClass]
public sealed class DisplayGeometryTests
{
    [TestMethod]
    public void Contains_PointInsideRectangle_ReturnsTrue()
    {
        PhysicalRectangle rectangle =
            new(
                Left: -1920,
                Top: 0,
                Right: 0,
                Bottom: 1080);

        Assert.IsTrue(
            rectangle.Contains(
                new PhysicalPoint(
                    -100,
                    500)));
    }

    [TestMethod]
    public void Contains_RightEdge_ReturnsFalse()
    {
        PhysicalRectangle rectangle =
            new(
                Left: 0,
                Top: 0,
                Right: 1920,
                Bottom: 1080);

        Assert.IsFalse(
            rectangle.Contains(
                new PhysicalPoint(
                    1920,
                    500)));
    }

    [TestMethod]
    public void DistanceSquaredTo_PointInsideRectangle_ReturnsZero()
    {
        PhysicalRectangle rectangle =
            new(
                Left: 0,
                Top: 0,
                Right: 100,
                Bottom: 100);

        double distance =
            rectangle.DistanceSquaredTo(
                new PhysicalPoint(
                    50,
                    50));

        Assert.AreEqual(
            0,
            distance);
    }
}
