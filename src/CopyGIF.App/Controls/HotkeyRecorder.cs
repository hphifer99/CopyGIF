using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace CopyGIF.App.Controls;

/// <summary>
/// Records a supported global shortcut from the keyboard instead of requiring the person to
/// type CopyGIF's persisted shortcut syntax. The Text value remains the canonical persisted
/// representation, for example Ctrl+Shift+G.
/// </summary>
public sealed class HotkeyRecorder : TextBox
{
    private string _valueBeforeRecording = string.Empty;

    public HotkeyRecorder()
    {
        DefaultStyleKey = typeof(TextBox);
        IsReadOnly = true;
        PlaceholderText = "Press a shortcut";
        AutomationProperties.SetHelpText(
            this,
            "Press one or more modifier keys and a supported key. Escape cancels. Backspace or Delete clears the shortcut.");

        GotFocus += (_, _) =>
        {
            _valueBeforeRecording = Text ?? string.Empty;
            SelectAll();
        };

        KeyDown += HandleKeyDown;
    }

    private void HandleKeyDown(
        object sender,
        KeyRoutedEventArgs args)
    {
        _ = sender;
        ArgumentNullException.ThrowIfNull(args);

        if (args.Key == VirtualKey.Escape)
        {
            Text = _valueBeforeRecording;
            SelectAll();
            args.Handled = true;
            return;
        }

        if (args.Key is VirtualKey.Back or VirtualKey.Delete && !HasAnyModifier())
        {
            Text = string.Empty;
            args.Handled = true;
            return;
        }

        if (IsModifier(args.Key))
        {
            args.Handled = true;
            return;
        }

        string? keyName = GetSupportedKeyName(args.Key);
        List<string> parts = [];

        if (IsDown(VirtualKey.Control))
        {
            parts.Add("Ctrl");
        }

        if (IsDown(VirtualKey.Menu))
        {
            parts.Add("Alt");
        }

        if (IsDown(VirtualKey.Shift))
        {
            parts.Add("Shift");
        }

        if (IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows))
        {
            parts.Add("Win");
        }

        if (keyName is not null && parts.Count > 0)
        {
            parts.Add(keyName);
            Text = string.Join('+', parts);
            SelectAll();
        }

        args.Handled = true;
    }

    private static bool IsDown(VirtualKey key) =>
        InputKeyboardSource
            .GetKeyStateForCurrentThread(key)
            .HasFlag(CoreVirtualKeyStates.Down);

    private static bool HasAnyModifier() =>
        IsDown(VirtualKey.Control) ||
        IsDown(VirtualKey.Menu) ||
        IsDown(VirtualKey.Shift) ||
        IsDown(VirtualKey.LeftWindows) ||
        IsDown(VirtualKey.RightWindows);

    private static bool IsModifier(VirtualKey key) =>
        key is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl or
            VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu or
            VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift or
            VirtualKey.LeftWindows or VirtualKey.RightWindows;

    private static string? GetSupportedKeyName(VirtualKey key)
    {
        int value = (int)key;

        if (value is >= (int)VirtualKey.A and <= (int)VirtualKey.Z)
        {
            return ((char)value).ToString();
        }

        if (value is >= (int)VirtualKey.Number0 and <= (int)VirtualKey.Number9)
        {
            return ((char)value).ToString();
        }

        if (value is >= (int)VirtualKey.F1 and <= (int)VirtualKey.F24)
        {
            return $"F{value - (int)VirtualKey.F1 + 1}";
        }

        return key switch
        {
            VirtualKey.Back => "Backspace",
            VirtualKey.Tab => "Tab",
            VirtualKey.Enter => "Enter",
            VirtualKey.Pause => "Pause",
            VirtualKey.CapitalLock => "CapsLock",
            VirtualKey.Space => "Space",
            VirtualKey.PageUp => "PageUp",
            VirtualKey.PageDown => "PageDown",
            VirtualKey.End => "End",
            VirtualKey.Home => "Home",
            VirtualKey.Left => "Left",
            VirtualKey.Up => "Up",
            VirtualKey.Right => "Right",
            VirtualKey.Down => "Down",
            VirtualKey.Snapshot => "PrintScreen",
            VirtualKey.Insert => "Insert",
            _ => null
        };
    }
}
