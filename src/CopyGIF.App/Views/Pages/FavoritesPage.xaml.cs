using System.Collections;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CopyGIF.App.Views.Pages;

public sealed partial class FavoritesPage :
    Page
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(FavoritesPage),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(FavoritesPage),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.Register(
            nameof(IsBusy),
            typeof(bool),
            typeof(FavoritesPage),
            new PropertyMetadata(
                false,
                HandleVisualStateChanged));

    public static readonly DependencyProperty IsEmptyProperty =
        DependencyProperty.Register(
            nameof(IsEmpty),
            typeof(bool),
            typeof(FavoritesPage),
            new PropertyMetadata(
                true,
                HandleVisualStateChanged));

    public static readonly DependencyProperty StatusMessageProperty =
        DependencyProperty.Register(
            nameof(StatusMessage),
            typeof(string),
            typeof(FavoritesPage),
            new PropertyMetadata(
                string.Empty,
                HandleStatusChanged));

    public static readonly DependencyProperty StatusSeverityProperty =
        DependencyProperty.Register(
            nameof(StatusSeverity),
            typeof(InfoBarSeverity),
            typeof(FavoritesPage),
            new PropertyMetadata(
                InfoBarSeverity.Informational));

    public static readonly DependencyProperty RefreshCommandProperty =
        DependencyProperty.Register(
            nameof(RefreshCommand),
            typeof(ICommand),
            typeof(FavoritesPage),
            new PropertyMetadata(null));

    public FavoritesPage()
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
            GetValue(
                RefreshCommandProperty)
                as ICommand;

        set =>
            SetValue(
                RefreshCommandProperty,
                value);
    }

    private static void HandleVisualStateChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        ((FavoritesPage)sender)
            .UpdateVisualState();
    }

    private static void HandleStatusChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        ((FavoritesPage)sender)
            .UpdateStatus();
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

        FavoritesGridView.Visibility =
            showEmptyState
                ? Visibility.Collapsed
                : Visibility.Visible;

        LoadingProgressRing.Visibility =
            IsBusy
                ? Visibility.Visible
                : Visibility.Collapsed;

        RefreshButton.IsEnabled =
            !IsBusy;
    }

    private void UpdateStatus()
    {
        PageStatusBanner.IsOpen =
            !string.IsNullOrWhiteSpace(
                StatusMessage);
    }
}
