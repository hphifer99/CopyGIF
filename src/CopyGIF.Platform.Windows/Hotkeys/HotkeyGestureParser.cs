using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace CopyGIF.Platform.Windows.Hotkeys;

internal static class HotkeyGestureParser
{
    private const int MaximumGestureLength = 100;

    private static readonly Dictionary<string, KeyDefinition>
        NamedKeys =
            new Dictionary<string, KeyDefinition>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["Backspace"] = new(0x08, "Backspace"),
                ["Tab"] = new(0x09, "Tab"),
                ["Enter"] = new(0x0D, "Enter"),
                ["Return"] = new(0x0D, "Enter"),
                ["Pause"] = new(0x13, "Pause"),
                ["CapsLock"] = new(0x14, "CapsLock"),
                ["Escape"] = new(0x1B, "Escape"),
                ["Esc"] = new(0x1B, "Escape"),
                ["Space"] = new(0x20, "Space"),
                ["PageUp"] = new(0x21, "PageUp"),
                ["PgUp"] = new(0x21, "PageUp"),
                ["PageDown"] = new(0x22, "PageDown"),
                ["PgDn"] = new(0x22, "PageDown"),
                ["End"] = new(0x23, "End"),
                ["Home"] = new(0x24, "Home"),
                ["Left"] = new(0x25, "Left"),
                ["Up"] = new(0x26, "Up"),
                ["Right"] = new(0x27, "Right"),
                ["Down"] = new(0x28, "Down"),
                ["PrintScreen"] = new(0x2C, "PrintScreen"),
                ["Insert"] = new(0x2D, "Insert"),
                ["Delete"] = new(0x2E, "Delete"),
                ["Del"] = new(0x2E, "Delete")
            };

    public static bool TryParse(
        string? text,
        [NotNullWhen(true)]
        out HotkeyGesture? gesture,
        out string errorMessage)
    {
        gesture = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            errorMessage =
                "Enter a hotkey such as Alt+G.";

            return false;
        }

        if (text.Length > MaximumGestureLength)
        {
            errorMessage =
                "The hotkey is too long.";

            return false;
        }

        string[] parts =
            text.Split(
                '+',
                StringSplitOptions.None);

        if (parts.Length < 2 ||
            parts.Any(
                static part =>
                    string.IsNullOrWhiteSpace(part)))
        {
            errorMessage =
                "Use one or more modifiers and one key, such as Alt+G.";

            return false;
        }

        HotkeyModifierFlags modifiers =
            HotkeyModifierFlags.None;

        KeyDefinition? key = null;

        foreach (string rawPart in parts)
        {
            string part = rawPart.Trim();

            if (TryGetModifier(
                    part,
                    out HotkeyModifierFlags modifier))
            {
                if ((modifiers & modifier) != 0)
                {
                    errorMessage =
                        $"The {GetModifierName(modifier)} modifier is listed more than once.";

                    return false;
                }

                modifiers |= modifier;
                continue;
            }

            if (key is not null)
            {
                errorMessage =
                    "A hotkey can contain only one non-modifier key.";

                return false;
            }

            if (!TryGetKey(
                    part,
                    out KeyDefinition parsedKey))
            {
                errorMessage =
                    $"'{part}' is not a supported hotkey key.";

                return false;
            }

            key = parsedKey;
        }

        if (modifiers == HotkeyModifierFlags.None)
        {
            errorMessage =
                "A global hotkey must include Ctrl, Alt, Shift, or Win.";

            return false;
        }

        if (key is null)
        {
            errorMessage =
                "The hotkey must include one non-modifier key.";

            return false;
        }

        KeyDefinition selectedKey =
            key.Value;

        gesture =
            new HotkeyGesture
            {
                Modifiers = modifiers,
                VirtualKey = selectedKey.VirtualKey,
                CanonicalText =
                    CreateCanonicalText(
                        modifiers,
                        selectedKey.CanonicalName)
            };

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryGetModifier(
        string part,
        out HotkeyModifierFlags modifier)
    {
        if (string.Equals(
                part,
                "Ctrl",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                part,
                "Control",
                StringComparison.OrdinalIgnoreCase))
        {
            modifier =
                HotkeyModifierFlags.Control;

            return true;
        }

        if (string.Equals(
                part,
                "Alt",
                StringComparison.OrdinalIgnoreCase))
        {
            modifier =
                HotkeyModifierFlags.Alt;

            return true;
        }

        if (string.Equals(
                part,
                "Shift",
                StringComparison.OrdinalIgnoreCase))
        {
            modifier =
                HotkeyModifierFlags.Shift;

            return true;
        }

        if (string.Equals(
                part,
                "Win",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                part,
                "Windows",
                StringComparison.OrdinalIgnoreCase))
        {
            modifier =
                HotkeyModifierFlags.Windows;

            return true;
        }

        modifier =
            HotkeyModifierFlags.None;

        return false;
    }

    private static bool TryGetKey(
        string part,
        out KeyDefinition key)
    {
        if (part.Length == 1)
        {
            char character =
                char.ToUpperInvariant(part[0]);

            if (character is >= 'A' and <= 'Z' ||
                character is >= '0' and <= '9')
            {
                key =
                    new KeyDefinition(
                        character,
                        character.ToString(
                            CultureInfo.InvariantCulture));

                return true;
            }
        }

        if (part.Length is 2 or 3 &&
            char.ToUpperInvariant(part[0]) == 'F' &&
            int.TryParse(
                part.AsSpan(1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int functionKey) &&
            functionKey is >= 1 and <= 24)
        {
            key =
                new KeyDefinition(
                    checked((uint)(
                        0x70 +
                        functionKey -
                        1)),
                    $"F{functionKey}");

            return true;
        }

        if (NamedKeys.TryGetValue(
                part,
                out KeyDefinition namedKey))
        {
            key = namedKey;
            return true;
        }

        key = default;
        return false;
    }

    private static string CreateCanonicalText(
        HotkeyModifierFlags modifiers,
        string keyName)
    {
        List<string> parts = new(5);

        if ((modifiers &
                HotkeyModifierFlags.Control) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers &
                HotkeyModifierFlags.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers &
                HotkeyModifierFlags.Shift) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers &
                HotkeyModifierFlags.Windows) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(keyName);
        return string.Join('+', parts);
    }

    private static string GetModifierName(
        HotkeyModifierFlags modifier)
    {
        return modifier switch
        {
            HotkeyModifierFlags.Control => "Ctrl",
            HotkeyModifierFlags.Alt => "Alt",
            HotkeyModifierFlags.Shift => "Shift",
            HotkeyModifierFlags.Windows => "Win",
            _ => "unknown"
        };
    }

    private readonly record struct KeyDefinition(
        uint VirtualKey,
        string CanonicalName);
}
