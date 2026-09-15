using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

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
