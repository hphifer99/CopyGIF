using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CopyGIF.App.Controls;

public sealed partial class StatusBanner :
    UserControl
{
    public static readonly DependencyProperty
        TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(StatusBanner),
                new PropertyMetadata(
                    string.Empty));

    public static readonly DependencyProperty
        MessageProperty =
            DependencyProperty.Register(
                nameof(Message),
                typeof(string),
                typeof(StatusBanner),
                new PropertyMetadata(
                    string.Empty));

    public static readonly DependencyProperty
        SeverityProperty =
            DependencyProperty.Register(
                nameof(Severity),
                typeof(InfoBarSeverity),
                typeof(StatusBanner),
                new PropertyMetadata(
                    InfoBarSeverity.Informational));

    public static readonly DependencyProperty
        IsOpenProperty =
            DependencyProperty.Register(
                nameof(IsOpen),
                typeof(bool),
                typeof(StatusBanner),
                new PropertyMetadata(
                    false));

    public static readonly DependencyProperty
        IsClosableProperty =
            DependencyProperty.Register(
                nameof(IsClosable),
                typeof(bool),
                typeof(StatusBanner),
                new PropertyMetadata(
                    false));

    public static readonly DependencyProperty
        ActionTextProperty =
            DependencyProperty.Register(
                nameof(ActionText),
                typeof(string),
                typeof(StatusBanner),
                new PropertyMetadata(
                    string.Empty,
                    HandleActionTextChanged));

    public static readonly DependencyProperty
        ActionCommandProperty =
            DependencyProperty.Register(
                nameof(ActionCommand),
                typeof(ICommand),
                typeof(StatusBanner),
                new PropertyMetadata(
                    null));

    public static readonly DependencyProperty
        ActionCommandParameterProperty =
            DependencyProperty.Register(
                nameof(ActionCommandParameter),
                typeof(object),
                typeof(StatusBanner),
                new PropertyMetadata(
                    null));

    public StatusBanner()
    {
        InitializeComponent();

        UpdateActionButton();
    }

    public string Title
    {
        get =>
            (string)GetValue(
                TitleProperty);

        set =>
            SetValue(
                TitleProperty,
                value);
    }

    public string Message
    {
        get =>
            (string)GetValue(
                MessageProperty);

        set =>
            SetValue(
                MessageProperty,
                value);
    }

    public InfoBarSeverity Severity
    {
        get =>
            (InfoBarSeverity)GetValue(
                SeverityProperty);

        set =>
            SetValue(
                SeverityProperty,
                value);
    }

    public bool IsOpen
    {
        get =>
            (bool)GetValue(
                IsOpenProperty);

        set =>
            SetValue(
                IsOpenProperty,
                value);
    }

    public bool IsClosable
    {
        get =>
            (bool)GetValue(
                IsClosableProperty);

        set =>
            SetValue(
                IsClosableProperty,
                value);
    }

    public string ActionText
    {
        get =>
            (string)GetValue(
                ActionTextProperty);

        set =>
            SetValue(
                ActionTextProperty,
                value);
    }

    public ICommand? ActionCommand
    {
        get =>
            GetValue(
                ActionCommandProperty)
                as ICommand;

        set =>
            SetValue(
                ActionCommandProperty,
                value);
    }

    public object? ActionCommandParameter
    {
        get =>
            GetValue(
                ActionCommandParameterProperty);

        set =>
            SetValue(
                ActionCommandParameterProperty,
                value);
    }

    private static void HandleActionTextChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        ((StatusBanner)sender)
            .UpdateActionButton();
    }

    private void UpdateActionButton()
    {
        StatusActionButton.Visibility =
            string.IsNullOrWhiteSpace(
                ActionText)
                ? Visibility.Collapsed
                : Visibility.Visible;
    }
}
