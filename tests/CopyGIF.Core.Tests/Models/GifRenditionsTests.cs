using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Core.Tests.Models;

[TestClass]
public sealed class GifRenditionsTests
{
    [TestMethod]
    public void Select_MissingProviderSizes_FallsBackToAnimatedGif()
    {
        Uri original = new("https://static.klipy.com/original.gif");
        Uri small = new("https://static.klipy.com/small.gif");
        GifRenditions sizes = new() { Low = small };

        Assert.AreEqual(small, sizes.Select(GifQuality.Minimum, original));
        Assert.AreEqual(small, sizes.Select(GifQuality.Medium, original));
        Assert.AreEqual(original, sizes.Select(GifQuality.Maximum, original));
    }
}
