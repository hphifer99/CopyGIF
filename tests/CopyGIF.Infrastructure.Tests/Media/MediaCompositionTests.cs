using CopyGIF.Core.Contracts;
using CopyGIF.Infrastructure;
using CopyGIF.Infrastructure.Media;
using Microsoft.Extensions.DependencyInjection;

namespace CopyGIF.Infrastructure.Tests.Media;

[TestClass]
public sealed class MediaCompositionTests
{
    [TestMethod]
    public void AddCopyGifInfrastructure_ResolvesSingletonPreviewCache()
    {
        ServiceCollection services =
            new();

        services.AddCopyGifInfrastructure();

        using ServiceProvider provider =
            services.BuildServiceProvider();

        IPreviewCache first =
            provider.GetRequiredService<
                IPreviewCache>();

        IPreviewCache second =
            provider.GetRequiredService<
                IPreviewCache>();

        Assert.IsInstanceOfType<
            PreviewCache>(
            first);

        Assert.AreSame(
            first,
            second);
    }

    [TestMethod]
    public void DefaultPreviewCacheLimits_MatchFrozenMediaPolicy()
    {
        PreviewCacheLimits limits =
            PreviewCacheLimits.Default;

        Assert.AreEqual(
            5L * 1024L * 1024L,
            limits.MaximumThumbnailBytes);

        Assert.AreEqual(
            20L * 1024L * 1024L,
            limits.MaximumPreviewBytes);

        Assert.AreEqual(
            256L * 1024L * 1024L,
            limits.MaximumThumbnailCacheBytes);

        Assert.AreEqual(
            512L * 1024L * 1024L,
            limits.MaximumPreviewCacheBytes);

        Assert.AreEqual(
            TimeSpan.FromDays(7),
            limits.Retention);
    }
}
