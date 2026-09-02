namespace CopyGIF.Platform.Windows.Hotkeys;

[Flags]
internal enum HotkeyModifierFlags : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008
}

internal sealed record HotkeyGesture
{
    public required HotkeyModifierFlags Modifiers { get; init; }

    public required uint VirtualKey { get; init; }

    public required string CanonicalText { get; init; }
}
