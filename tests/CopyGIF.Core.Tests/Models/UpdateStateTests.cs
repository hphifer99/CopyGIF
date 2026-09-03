using CopyGIF.Core.Models;

namespace CopyGIF.Core.Tests.Models;

[TestClass]
public sealed class UpdateStateTests
{
    [TestMethod]
    public void Defaults_RepresentNoCompletedUpdateWork()
    {
        UpdateState state = new();

        Assert.AreEqual(
            UpdateState.CurrentSchemaVersion,
            state.SchemaVersion);

        Assert.IsFalse(
            state.HasCompletedCheck);

        Assert.IsFalse(
            state.HasDownloadedUpdate);
    }

    [TestMethod]
    public void CompletedCheck_IsReportedWhenTimestampExists()
    {
        UpdateState state = new()
        {
            LastCheckedAtUtc =
                new DateTimeOffset(
                    2026,
                    9,
                    3,
                    12,
                    0,
                    0,
                    TimeSpan.Zero),

            LastAvailableVersion =
                "2.0.1"
        };

        Assert.IsTrue(
            state.HasCompletedCheck);

        Assert.AreEqual(
            "2.0.1",
            state.LastAvailableVersion);
    }

    [TestMethod]
    public void DownloadedUpdate_RequiresVersionAndTimestamp()
    {
        UpdateState versionOnly = new()
        {
            LastDownloadedVersion =
                "2.0.1"
        };

        UpdateState complete = versionOnly with
        {
            LastDownloadedAtUtc =
                new DateTimeOffset(
                    2026,
                    9,
                    3,
                    12,
                    30,
                    0,
                    TimeSpan.Zero)
        };

        Assert.IsFalse(
            versionOnly.HasDownloadedUpdate);

        Assert.IsTrue(
            complete.HasDownloadedUpdate);
    }
}
