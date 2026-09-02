using CopyGIF.Platform.Windows.Hotkeys;

namespace CopyGIF.Platform.Windows.Tests.Hotkeys;

[TestClass]
public sealed class HotkeyGestureParserTests
{
    [TestMethod]
    public void TryParse_DefaultGesture_ReturnsAltAndG()
    {
        bool succeeded =
            HotkeyGestureParser.TryParse(
                "Alt+G",
                out HotkeyGesture? gesture,
                out string errorMessage);

        Assert.IsTrue(succeeded);

        Assert.AreEqual(
            string.Empty,
            errorMessage);

        Assert.IsNotNull(gesture);

        Assert.AreEqual(
            HotkeyModifierFlags.Alt,
            gesture.Modifiers);

        Assert.AreEqual(
            (uint)'G',
            gesture.VirtualKey);

        Assert.AreEqual(
            "Alt+G",
            gesture.CanonicalText);
    }

    [TestMethod]
    public void TryParse_AliasesAndDifferentOrder_NormalizesGesture()
    {
        bool succeeded =
            HotkeyGestureParser.TryParse(
                "windows + shift + control + pgdn",
                out HotkeyGesture? gesture,
                out _);

        Assert.IsTrue(succeeded);
        Assert.IsNotNull(gesture);

        Assert.AreEqual(
            "Ctrl+Shift+Win+PageDown",
            gesture.CanonicalText);
    }

    [TestMethod]
    public void TryParse_FunctionKey_ReturnsVirtualKey()
    {
        bool succeeded =
            HotkeyGestureParser.TryParse(
                "Ctrl+F24",
                out HotkeyGesture? gesture,
                out _);

        Assert.IsTrue(succeeded);
        Assert.IsNotNull(gesture);

        Assert.AreEqual(
            0x87U,
            gesture.VirtualKey);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("G")]
    [DataRow("Alt+")]
    [DataRow("Alt+Ctrl")]
    [DataRow("Alt+Alt+G")]
    [DataRow("Alt+G+H")]
    [DataRow("Alt+F25")]
    [DataRow("Alt+UnknownKey")]
    public void TryParse_InvalidGesture_ReturnsHelpfulError(
        string text)
    {
        bool succeeded =
            HotkeyGestureParser.TryParse(
                text,
                out HotkeyGesture? gesture,
                out string errorMessage);

        Assert.IsFalse(succeeded);
        Assert.IsNull(gesture);

        Assert.IsFalse(
            string.IsNullOrWhiteSpace(
                errorMessage));
    }
}
