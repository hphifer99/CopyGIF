using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace CopyGIF.App.Services;

internal static class WindowTitleBar
{
    private static readonly Color DarkForeground =
        Color.FromArgb(
            255,
            255,
            255,
            255);

    private static readonly Color LightForeground =
        Color.FromArgb(
            255,
            0,
            0,
            0);

    public static void Apply(
        Window window,
        FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(
            window);

        ArgumentNullException.ThrowIfNull(
            root);

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "CopyGif.ico");
        if (File.Exists(iconPath)) window.AppWindow.SetIcon(iconPath);
        AccessibilitySettings accessibilitySettings = new();
        bool highContrastSubscribed = false;
        bool closed = false;
        try
        {
            accessibilitySettings.HighContrastChanged += HandleHighContrastChanged;
            highContrastSubscribed = true;
        }
        catch (COMException exception)
        {
            Debug.WriteLine($"Unable to monitor title bar contrast changes: {exception.Message}");
        }
        window.Closed += HandleClosed;

        void HandleHighContrastChanged(AccessibilitySettings sender, object args)
        {
            root.DispatcherQueue.TryEnqueue(() =>
            {
                if (!closed)
                {
                    ApplyColors(window, root);
                }
            });
        }

        void HandleClosed(object sender, WindowEventArgs args)
        {
            closed = true;
            if (highContrastSubscribed)
            {
                try
                {
                    accessibilitySettings.HighContrastChanged -= HandleHighContrastChanged;
                }
                catch (COMException exception)
                {
                    Debug.WriteLine($"Unable to stop monitoring title bar contrast changes: {exception.Message}");
                }
            }
            window.Closed -= HandleClosed;
        }

        ApplyColors(
            window,
            root);

        root.Loaded +=
            (_, _) =>
                ApplyColors(
                    window,
                    root);

        root.ActualThemeChanged +=
            (_, _) =>
                ApplyColors(
                    window,
                    root);
    }

    private static void ApplyColors(
        Window window,
        FrameworkElement root)
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        Color background =
            root is Panel panel &&
            panel.Background is SolidColorBrush brush
                ? brush.Color
                : root.ActualTheme == ElementTheme.Dark
                    ? Color.FromArgb(
                        255,
                        32,
                        32,
                        32)
                    : Color.FromArgb(
                        255,
                        243,
                        243,
                        243);

        Color foreground =
            root.ActualTheme == ElementTheme.Dark
                ? DarkForeground
                : LightForeground;

        Color hover =
            root.ActualTheme == ElementTheme.Dark
                ? Color.FromArgb(
                    255,
                    54,
                    54,
                    54)
                : Color.FromArgb(
                    255,
                    229,
                    229,
                    229);

        Color pressed =
            root.ActualTheme == ElementTheme.Dark
                ? Color.FromArgb(
                    255,
                    68,
                    68,
                    68)
                : Color.FromArgb(
                    255,
                    215,
                    215,
                    215);

        try
        {
            if (new AccessibilitySettings().HighContrast)
            {
                // Null restores Windows-owned caption colors for every
                // high-contrast palette, including custom user palettes.
                AppWindowTitleBar titleBar = window.AppWindow.TitleBar;
                titleBar.BackgroundColor = null;
                titleBar.InactiveBackgroundColor = null;
                titleBar.ForegroundColor = null;
                titleBar.InactiveForegroundColor = null;
                titleBar.ButtonBackgroundColor = null;
                titleBar.ButtonInactiveBackgroundColor = null;
                titleBar.ButtonForegroundColor = null;
                titleBar.ButtonInactiveForegroundColor = null;
                titleBar.ButtonHoverBackgroundColor = null;
                titleBar.ButtonHoverForegroundColor = null;
                titleBar.ButtonPressedBackgroundColor = null;
                titleBar.ButtonPressedForegroundColor = null;
                return;
            }

            window.AppWindow.TitleBar.BackgroundColor =
                background;

            window.AppWindow.TitleBar.InactiveBackgroundColor =
                background;

            window.AppWindow.TitleBar.ForegroundColor =
                foreground;

            window.AppWindow.TitleBar.InactiveForegroundColor =
                foreground;

            window.AppWindow.TitleBar.ButtonBackgroundColor =
                background;

            window.AppWindow.TitleBar.ButtonInactiveBackgroundColor =
                background;

            window.AppWindow.TitleBar.ButtonForegroundColor =
                foreground;

            window.AppWindow.TitleBar.ButtonInactiveForegroundColor =
                foreground;

            window.AppWindow.TitleBar.ButtonHoverBackgroundColor =
                hover;

            window.AppWindow.TitleBar.ButtonHoverForegroundColor =
                foreground;

            window.AppWindow.TitleBar.ButtonPressedBackgroundColor =
                pressed;

            window.AppWindow.TitleBar.ButtonPressedForegroundColor =
                foreground;
        }
        catch (COMException exception)
        {
            Debug.WriteLine(
                $"Unable to apply the CopyGIF title bar colors: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            Debug.WriteLine(
                $"Unable to apply the CopyGIF title bar colors: {exception.Message}");
        }
        catch (NotSupportedException exception)
        {
            Debug.WriteLine(
                $"Unable to apply the CopyGIF title bar colors: {exception.Message}");
        }
    }
}
