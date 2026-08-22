using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace CopyGIF.Services
{
    public sealed class HotkeyService : IDisposable
    {
        private const int HotkeyId = 0x4347;
        private const int WmHotkey = 0x0312;

        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;
        private const uint ModNoRepeat = 0x4000;

        private IntPtr _windowHandle;
        private HwndSource _source;
        private Action _hotkeyPressed;
        private bool _registered;

        public bool IsRegistered => _registered;

        public void Register(
            Window window,
            string hotkey,
            Action hotkeyPressed)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (hotkeyPressed == null)
            {
                throw new ArgumentNullException(nameof(hotkeyPressed));
            }

            if (_registered)
            {
                throw new InvalidOperationException(
                    "A hotkey is already registered.");
            }

            ParseHotkey(
                hotkey,
                out uint modifiers,
                out uint virtualKey,
                out string normalizedHotkey);

            _windowHandle =
                new WindowInteropHelper(window).Handle;

            if (_windowHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "The application window is not ready.");
            }

            _source = HwndSource.FromHwnd(_windowHandle);

            if (_source == null)
            {
                throw new InvalidOperationException(
                    "Could not access the application window.");
            }

            _source.AddHook(WindowMessageHook);
            _hotkeyPressed = hotkeyPressed;

            if (!RegisterHotKey(
                    _windowHandle,
                    HotkeyId,
                    modifiers,
                    virtualKey))
            {
                int errorCode = Marshal.GetLastWin32Error();

                _source.RemoveHook(WindowMessageHook);
                _source = null;
                _hotkeyPressed = null;
                _windowHandle = IntPtr.Zero;

                throw new InvalidOperationException(
                    "Could not register " + normalizedHotkey +
                    ". Another application may already be using it. " +
                    "Windows error: " + errorCode);
            }

            _registered = true;
        }

        public void Unregister()
        {
            if (_registered)
            {
                UnregisterHotKey(_windowHandle, HotkeyId);
                _registered = false;
            }

            if (_source != null)
            {
                _source.RemoveHook(WindowMessageHook);
                _source = null;
            }

            _hotkeyPressed = null;
            _windowHandle = IntPtr.Zero;
        }

        public static string Normalize(string hotkey)
        {
            ParseHotkey(
                hotkey,
                out uint ignoredModifiers,
                out uint ignoredVirtualKey,
                out string normalizedHotkey);

            return normalizedHotkey;
        }

        private IntPtr WindowMessageHook(
            IntPtr windowHandle,
            int message,
            IntPtr wordParameter,
            IntPtr longParameter,
            ref bool handled)
        {
            if (message == WmHotkey &&
                wordParameter.ToInt32() == HotkeyId)
            {
                handled = true;
                _hotkeyPressed?.Invoke();
            }

            return IntPtr.Zero;
        }

        private static void ParseHotkey(
            string hotkey,
            out uint modifiers,
            out uint virtualKey,
            out string normalizedHotkey)
        {
            if (string.IsNullOrWhiteSpace(hotkey))
            {
                throw new ArgumentException(
                    "A hotkey is required.",
                    nameof(hotkey));
            }

            modifiers = ModNoRepeat;
            bool hasModifier = false;
            bool hasAlt = false;
            bool hasControl = false;
            bool hasShift = false;
            bool hasWin = false;
            Key selectedKey = Key.None;

            string[] parts = hotkey.Split('+');

            foreach (string originalPart in parts)
            {
                string part = originalPart.Trim();

                if (part.Length == 0)
                {
                    throw new ArgumentException(
                        "The hotkey contains an empty part.");
                }

                switch (part.ToUpperInvariant())
                {
                    case "ALT":
                        modifiers |= ModAlt;
                        hasAlt = true;
                        hasModifier = true;
                        break;

                    case "CTRL":
                    case "CONTROL":
                        modifiers |= ModControl;
                        hasControl = true;
                        hasModifier = true;
                        break;

                    case "SHIFT":
                        modifiers |= ModShift;
                        hasShift = true;
                        hasModifier = true;
                        break;

                    case "WIN":
                    case "WINDOWS":
                        modifiers |= ModWin;
                        hasWin = true;
                        hasModifier = true;
                        break;

                    default:
                        if (selectedKey != Key.None)
                        {
                            throw new ArgumentException(
                                "The hotkey contains more than one key.");
                        }

                        try
                        {
                            var converter = new KeyConverter();
                            selectedKey =
                                (Key)converter.ConvertFromString(part);
                        }
                        catch
                        {
                            throw new ArgumentException(
                                "The hotkey contains an invalid key: " + part);
                        }

                        break;
                }
            }

            if (!hasModifier)
            {
                throw new ArgumentException(
                    "Use at least one Ctrl, Alt, Shift, or Win modifier.");
            }

            if (selectedKey == Key.None)
            {
                throw new ArgumentException(
                    "The hotkey does not contain a key.");
            }

            int keyCode = KeyInterop.VirtualKeyFromKey(selectedKey);

            if (keyCode <= 0)
            {
                throw new ArgumentException(
                    "The hotkey key could not be recognized.");
            }

            virtualKey = (uint)keyCode;

            var normalizedParts = new List<string>();

            if (hasControl)
            {
                normalizedParts.Add("Ctrl");
            }

            if (hasAlt)
            {
                normalizedParts.Add("Alt");
            }

            if (hasShift)
            {
                normalizedParts.Add("Shift");
            }

            if (hasWin)
            {
                normalizedParts.Add("Win");
            }

            normalizedParts.Add(selectedKey.ToString());
            normalizedHotkey = string.Join("+", normalizedParts);
        }

        public void Dispose()
        {
            Unregister();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(
            IntPtr windowHandle,
            int id,
            uint modifiers,
            uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(
            IntPtr windowHandle,
            int id);
    }
}

