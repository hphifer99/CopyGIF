using CopyGIF.Presentation.Common;

namespace CopyGIF.Presentation.Tests.Common;

[TestClass]
public sealed class UserMessageTests
{
    [TestMethod]
    public void Information_CreatesInformationMessage()
    {
        UserMessage message =
            UserMessage.Information(
                "  Ready.  ",
                "  ready  ");

        Assert.AreEqual(
            "Ready.",
            message.Text);

        Assert.AreEqual(
            UserMessageSeverity.Information,
            message.Severity);

        Assert.AreEqual(
            "ready",
            message.Code);

        Assert.IsFalse(
            message.IsError);

        Assert.IsFalse(
            message.IsWarning);
    }

    [TestMethod]
    public void Success_CreatesSuccessMessage()
    {
        UserMessage message =
            UserMessage.Success(
                "Saved.");

        Assert.AreEqual(
            UserMessageSeverity.Success,
            message.Severity);

        Assert.AreEqual(
            "Saved.",
            message.Text);

        Assert.IsNull(
            message.Code);

        Assert.IsFalse(
            message.IsError);
    }

    [TestMethod]
    public void Warning_CreatesWarningMessage()
    {
        UserMessage message =
            UserMessage.Warning(
                "Limit reached.");

        Assert.AreEqual(
            UserMessageSeverity.Warning,
            message.Severity);

        Assert.IsTrue(
            message.IsWarning);

        Assert.IsFalse(
            message.IsError);
    }

    [TestMethod]
    public void Error_CreatesErrorMessage()
    {
        UserMessage message =
            UserMessage.Error(
                "Unable to save.",
                "save_failed");

        Assert.AreEqual(
            UserMessageSeverity.Error,
            message.Severity);

        Assert.IsTrue(
            message.IsError);

        Assert.IsFalse(
            message.IsWarning);

        Assert.AreEqual(
            "save_failed",
            message.Code);
    }

    [TestMethod]
    public void Factory_RejectsEmptyText()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () =>
                UserMessage.Information(
                    "   "));
    }

    [TestMethod]
    public void Factory_NormalizesEmptyCodeToNull()
    {
        UserMessage message =
            UserMessage.Error(
                "Failure.",
                "   ");

        Assert.IsNull(
            message.Code);
    }
}
