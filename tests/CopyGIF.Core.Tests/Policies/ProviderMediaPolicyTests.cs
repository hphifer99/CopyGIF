using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;

namespace CopyGIF.Core.Tests.Policies;

// These tests change or read process-wide state (the configured providers and the repair log
// sink), so they must not run at the same time as other tests.
[TestClass]
[DoNotParallelize]
public sealed class ProviderMediaPolicyTests
{
    private static readonly ProviderDescriptor Third = new()
    {
        Id = "third",
        DisplayName = "Third Gifs",
        MediaHosts = ["cdn.third.example"],
        UsesDirectPreview = true,
        AllowsPersistentLibrary = true
    };

    [TestCleanup]
    public void RestoreBuiltInProviders() =>
        ProviderMediaPolicy.Configure(BuiltInProviders.All);

    [TestMethod]
    [DataRow("klipy", true)]
    [DataRow("KLIPY", true)]
    [DataRow(" giphy ", true)]
    [DataRow("giphy", true)]
    [DataRow("unknown", false)]
    [DataRow("", false)]
    public void AllowsPersistentLibrary_BuiltInProviders_KeepWorkingForBoth(string providerId, bool expected)
    {
        Assert.AreEqual(expected, ProviderMediaPolicy.AllowsPersistentLibrary(providerId));
    }

    [TestMethod]
    public void UsesDirectPreview_OnlyGiphyDoes()
    {
        Assert.IsTrue(ProviderMediaPolicy.UsesDirectPreview("giphy"));
        Assert.IsFalse(ProviderMediaPolicy.UsesDirectPreview("klipy"));
        Assert.IsFalse(ProviderMediaPolicy.UsesDirectPreview("unknown"));
    }

    [TestMethod]
    [DataRow("https://media.giphy.com/media/a/giphy.gif", true)]
    [DataRow("https://media0.giphy.com/media/a/giphy.gif", true)]
    [DataRow("https://media4.giphy.com/media/a/giphy.gif", true)]
    [DataRow("https://MEDIA2.GIPHY.COM/media/a/giphy.gif", true)]
    [DataRow("https://media5.giphy.com/media/a/giphy.gif", false)]
    [DataRow("https://i.giphy.com/a.gif", false)]
    [DataRow("http://media.giphy.com/media/a/giphy.gif", false)]
    [DataRow("https://media.giphy.com:8443/media/a/giphy.gif", false)]
    [DataRow("https://user@media.giphy.com/media/a/giphy.gif", false)]
    [DataRow("https://media.giphy.com/media/a/giphy.gif#frag", false)]
    // KLIPY does not load directly, so its hosts are not direct media hosts.
    [DataRow("https://static.klipy.com/ii/a/b.gif", false)]
    [DataRow("https://example.com/a.gif", false)]
    public void IsDirectMediaUri_AcceptsOnlyPlainHttpsAddressesOnDirectPreviewHosts(string address, bool expected)
    {
        Assert.AreEqual(expected, ProviderMediaPolicy.IsDirectMediaUri(new Uri(address)));
    }

    [TestMethod]
    public void IsDirectMediaUri_NullAndRelative_AreRejected()
    {
        Assert.IsFalse(ProviderMediaPolicy.IsDirectMediaUri(null));
        Assert.IsFalse(ProviderMediaPolicy.IsDirectMediaUri(new Uri("/media/a.gif", UriKind.Relative)));
    }

    [TestMethod]
    public void DisplayNameFor_UsesTheDescriptorName_AndFallsBackToTheIdAsGiven()
    {
        Assert.AreEqual("KLIPY", ProviderMediaPolicy.DisplayNameFor("klipy"));
        Assert.AreEqual("GIPHY", ProviderMediaPolicy.DisplayNameFor("giphy"));
        Assert.AreEqual("old-provider", ProviderMediaPolicy.DisplayNameFor(" old-provider "));
        Assert.AreEqual(string.Empty, ProviderMediaPolicy.DisplayNameFor(null));
    }

    [TestMethod]
    public void Configure_AProviderAddedLater_NeedsNoChangeToThePolicy()
    {
        Assert.IsFalse(ProviderMediaPolicy.AllowsPersistentLibrary("third"));
        Assert.IsFalse(ProviderMediaPolicy.IsDirectMediaUri(new Uri("https://cdn.third.example/a.gif")));

        ProviderMediaPolicy.Configure([.. BuiltInProviders.All, Third]);

        Assert.IsTrue(ProviderMediaPolicy.AllowsPersistentLibrary("third"));
        Assert.IsTrue(ProviderMediaPolicy.UsesDirectPreview("third"));
        Assert.IsTrue(ProviderMediaPolicy.IsDirectMediaUri(new Uri("https://cdn.third.example/a.gif")));
        Assert.AreEqual("Third Gifs", ProviderMediaPolicy.DisplayNameFor("third"));
        // The built-in providers are unaffected.
        Assert.IsTrue(ProviderMediaPolicy.IsDirectMediaUri(new Uri("https://media.giphy.com/media/a/giphy.gif")));
        Assert.IsTrue(ProviderMediaPolicy.AllowsPersistentLibrary("klipy"));
    }

    [TestMethod]
    public void Configure_AProviderThatDoesNotOptIn_CannotKeepGifs()
    {
        ProviderMediaPolicy.Configure(
            [.. BuiltInProviders.All, new ProviderDescriptor { Id = "cautious", DisplayName = "Cautious" }]);

        Assert.IsFalse(ProviderMediaPolicy.AllowsPersistentLibrary("cautious"));
        Assert.IsFalse(ProviderMediaPolicy.UsesDirectPreview("cautious"));
    }

    [TestMethod]
    public void Configure_WithoutProviders_IsRejected_AndKeepsTheCurrentRules()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ProviderMediaPolicy.Configure([]));

        Assert.IsTrue(ProviderMediaPolicy.AllowsPersistentLibrary("giphy"));
    }

    [TestMethod]
    public void RecordRejectedHost_WritesTheHostNameAndNothingElse()
    {
        string line = CaptureLine(
            () => ProviderMediaPolicy.RecordRejectedHost(
                "giphy",
                new Uri("https://media9.giphy.com/media/secret-path/giphy.gif?token=abc123")));

        StringAssert.Contains(line, "stage=media-host-rejected");
        StringAssert.Contains(line, "provider=giphy");
        StringAssert.Contains(line, "unlisted:media9.giphy.com");
        Assert.IsFalse(line.Contains("secret-path", StringComparison.Ordinal));
        Assert.IsFalse(line.Contains("abc123", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RecordRejectedHost_WithNoAddress_SaysSo()
    {
        string line = CaptureLine(() => ProviderMediaPolicy.RecordRejectedHost("giphy", null));

        StringAssert.Contains(line, "unlisted:unparsable");
    }

    private static string CaptureLine(Action action)
    {
        string captured = string.Empty;
        Action<string>? previous = RepairDiagnostics.Sink;
        RepairDiagnostics.Sink = message => captured = message;
        try
        {
            action();
        }
        finally
        {
            RepairDiagnostics.Sink = previous;
        }

        return captured;
    }
}
