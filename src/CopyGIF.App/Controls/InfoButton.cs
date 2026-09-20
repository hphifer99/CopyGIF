using CopyGIF.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace CopyGIF.App.Controls;

/// <summary>
/// The small "i" button that explains a setting. The explanation opens as soon as the pointer
/// enters the button or the button receives keyboard focus, and it also opens when the button is
/// clicked or tapped, so it is reachable with a mouse, a keyboard, and touch. It closes when the
/// pointer leaves, when focus moves away, or when Escape is pressed. It does not rely on the
/// operating system hover delay alone.
/// </summary>
public sealed class InfoButton : Button
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(InfoButton),
            new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public static readonly DependencyProperty SubjectProperty =
        DependencyProperty.Register(
            nameof(Subject),
            typeof(string),
            typeof(InfoButton),
            new PropertyMetadata(string.Empty, OnDescriptionChanged));

    private readonly ToolTip _tip;

    private readonly TextBlock _tipText;

    public InfoButton()
    {
        // Look exactly like a normal Button. Without this, the derived type would look for its own
        // default style and find none.
        DefaultStyleKey = typeof(Button);

        Width = 28;
        Height = 28;
        MinWidth = 0;
        MinHeight = 0;
        Padding = new Thickness(0);
        VerticalAlignment = VerticalAlignment.Center;

        Content = new FontIcon
        {
            Glyph = "\uE946",
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 14
        };

        _tipText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 340
        };

        _tip = new ToolTip
        {
            Content = _tipText
        };

        // The normal tooltip stays in place as a fallback. The handlers below open the same
        // tooltip earlier and also on keyboard focus and on click.
        ToolTipService.SetToolTip(this, _tip);

        PointerEntered += (_, _) => SetOpen(true);
        PointerExited += (_, _) => SetOpen(false);
        PointerCanceled += (_, _) => SetOpen(false);
        PointerCaptureLost += (_, _) => SetOpen(false);
        GotFocus += (_, _) =>
        {
            if (FocusState == FocusState.Keyboard)
            {
                SetOpen(true);
            }
        };
        LostFocus += (_, _) => SetOpen(false);
        Click += (_, _) => SetOpen(true);
        KeyDown += OnKeyDown;
    }

    /// <summary>The explanation shown to the person.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The name of the setting that is explained, used for screen readers.</summary>
    public string Subject
    {
        get => (string)GetValue(SubjectProperty);
        set => SetValue(SubjectProperty, value);
    }

    private static void OnDescriptionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        _ = args;

        ((InfoButton)sender).UpdateDescription();
    }

    private void UpdateDescription()
    {
        _tipText.Text = Text ?? string.Empty;

        AutomationProperties.SetName(
            this,
            string.IsNullOrWhiteSpace(Subject) ? "Information" : $"{Subject} information");
        AutomationProperties.SetHelpText(this, Text ?? string.Empty);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        _ = sender;

        if (args.Key == Windows.System.VirtualKey.Escape && _tip.IsOpen)
        {
            SetOpen(false);
            args.Handled = true;
        }
    }

    private void SetOpen(bool open)
    {
        try
        {
            if (open && string.IsNullOrWhiteSpace(Text))
            {
                return;
            }

            if (_tip.IsOpen != open)
            {
                _tip.IsOpen = open;
            }
        }
        catch (Exception exception)
        {
            // A failed tooltip must never take the Settings window down.
            RepairDiagnostics.RecordException("info-button", exception);
        }
    }
}
