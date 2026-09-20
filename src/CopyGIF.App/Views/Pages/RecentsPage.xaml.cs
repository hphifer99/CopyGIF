using System.Collections;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CopyGIF.App.Views.Pages;

public sealed partial class RecentsPage :
    Page
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(RecentsPage),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(RecentsPage),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.Register(
            nameof(IsBusy),
            typeof(bool),
            typeof(RecentsPage),
            new PropertyMetadata(
                false,
                HandleVisualStateChanged));

    public static readonly DependencyProperty IsEmptyProperty =
        DependencyProperty.Register(
            nameof(IsEmpty),
            typeof(bool),
            typeof(RecentsPage),
            new PropertyMetadata(
                true,
                HandleVisualStateChanged));

    public static readonly DependencyProperty StatusMessageProperty =
        DependencyProperty.Register(
            nameof(StatusMessage),
            typeof(string),
            typeof(RecentsPage),
            new PropertyMetadata(
                string.Empty,
                HandleStatusChanged));

    public static readonly DependencyProperty StatusSeverityProperty =
        DependencyProperty.Register(
            nameof(StatusSeverity),
            typeof(InfoBarSeverity),
            typeof(RecentsPage),
            new PropertyMetadata(
                InfoBarSeverity.Informational));

    public static readonly DependencyProperty RefreshCommandProperty =
        RegisterCommand(
            nameof(RefreshCommand));

    public static readonly DependencyProperty ClearHistoryCommandProperty =
        RegisterCommand(
            nameof(ClearHistoryCommand));

    public RecentsPage()
    {
        InitializeComponent();

        UpdateVisualState();
        UpdateStatus();
    }

    public IEnumerable? ItemsSource
    {
        get =>
            GetValue(
                ItemsSourceProperty)
                as IEnumerable;

        set =>
            SetValue(
                ItemsSourceProperty,
                value);
    }

    public DataTemplate? ItemTemplate
    {
        get =>
            GetValue(
                ItemTemplateProperty)
                as DataTemplate;

        set =>
            SetValue(
                ItemTemplateProperty,
                value);
    }

    public bool IsBusy
    {
        get =>
            (bool)GetValue(
                IsBusyProperty);

        set =>
            SetValue(
                IsBusyProperty,
                value);
    }

    public bool IsEmpty
    {
        get =>
            (bool)GetValue(
                IsEmptyProperty);

        set =>
            SetValue(
                IsEmptyProperty,
                value);
    }

    public string StatusMessage
    {
        get =>
            (string)GetValue(
                StatusMessageProperty);

        set =>
            SetValue(
                StatusMessageProperty,
                value);
    }

    public InfoBarSeverity StatusSeverity
    {
        get =>
            (InfoBarSeverity)GetValue(
                StatusSeverityProperty);

        set =>
            SetValue(
                StatusSeverityProperty,
                value);
    }

    public ICommand? RefreshCommand
    {
        get =>
            GetCommand(
                RefreshCommandProperty);

        set =>
            SetValue(
                RefreshCommandProperty,
                value);
    }

    public ICommand? ClearHistoryCommand
    {
        get =>
            GetCommand(
                ClearHistoryCommandProperty);

        set =>
            SetValue(
                ClearHistoryCommandProperty,
                value);
    }

    private static DependencyProperty RegisterCommand(
        string propertyName)
    {
        return DependencyProperty.Register(
            propertyName,
            typeof(ICommand),
            typeof(RecentsPage),
            new PropertyMetadata(null));
    }

    private static void HandleVisualStateChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        ((RecentsPage)sender)
            .UpdateVisualState();
    }

    private static void HandleStatusChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        ((RecentsPage)sender)
            .UpdateStatus();
    }

    private ICommand? GetCommand(
        DependencyProperty property)
    {
        return GetValue(
            property)
                as ICommand;
    }

    private void UpdateVisualState()
    {
        bool showEmptyState =
            IsEmpty &&
            !IsBusy;

        EmptyStatePanel.Visibility =
            showEmptyState
                ? Visibility.Visible
                : Visibility.Collapsed;

        RecentsGridView.Visibility =
            showEmptyState
                ? Visibility.Collapsed
                : Visibility.Visible;

        LoadingProgressRing.Visibility =
            IsBusy
                ? Visibility.Visible
                : Visibility.Collapsed;

        RefreshButton.IsEnabled =
            !IsBusy;

        ClearHistoryButton.IsEnabled =
            !IsBusy &&
            !IsEmpty;
    }

    private void UpdateStatus()
    {
        PageStatusBanner.IsOpen =
            !string.IsNullOrWhiteSpace(
                StatusMessage);
    }
}
