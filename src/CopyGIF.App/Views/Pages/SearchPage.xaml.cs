using System.Collections;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CopyGIF.App.Views.Pages;

public sealed partial class SearchPage :
    Page
{
    public static readonly DependencyProperty QueryProperty =
        DependencyProperty.Register(
            nameof(Query),
            typeof(string),
            typeof(SearchPage),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SuggestionsProperty =
        DependencyProperty.Register(
            nameof(Suggestions),
            typeof(IEnumerable),
            typeof(SearchPage),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(SearchPage),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ItemTemplateProperty =
        DependencyProperty.Register(
            nameof(ItemTemplate),
            typeof(DataTemplate),
            typeof(SearchPage),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsBusyProperty =
        DependencyProperty.Register(
            nameof(IsBusy),
            typeof(bool),
            typeof(SearchPage),
            new PropertyMetadata(
                false,
                HandleVisualStateChanged));

    public static readonly DependencyProperty IsEmptyProperty =
        DependencyProperty.Register(
            nameof(IsEmpty),
            typeof(bool),
            typeof(SearchPage),
            new PropertyMetadata(
                true,
                HandleVisualStateChanged));

    public static readonly DependencyProperty CanLoadMoreProperty =
        DependencyProperty.Register(
            nameof(CanLoadMore),
            typeof(bool),
            typeof(SearchPage),
            new PropertyMetadata(
                false,
                HandleVisualStateChanged));

    public static readonly DependencyProperty StatusMessageProperty =
        DependencyProperty.Register(
            nameof(StatusMessage),
            typeof(string),
            typeof(SearchPage),
            new PropertyMetadata(
                string.Empty,
                HandleStatusChanged));

    public static readonly DependencyProperty StatusSeverityProperty =
        DependencyProperty.Register(
            nameof(StatusSeverity),
            typeof(InfoBarSeverity),
            typeof(SearchPage),
            new PropertyMetadata(
                InfoBarSeverity.Informational));

    public static readonly DependencyProperty SearchCommandProperty =
        RegisterCommand(
            nameof(SearchCommand));

    public static readonly DependencyProperty DebouncedSearchCommandProperty =
        RegisterCommand(
            nameof(DebouncedSearchCommand));

    public static readonly DependencyProperty RefreshSuggestionsCommandProperty =
        RegisterCommand(
            nameof(RefreshSuggestionsCommand));

    public static readonly DependencyProperty ClearQueryCommandProperty =
        RegisterCommand(
            nameof(ClearQueryCommand));

    public static readonly DependencyProperty CancelCommandProperty =
        RegisterCommand(
            nameof(CancelCommand));

    public static readonly DependencyProperty LoadMoreCommandProperty =
        RegisterCommand(
            nameof(LoadMoreCommand));

    public SearchPage()
    {
        InitializeComponent();

        UpdateVisualState();
        UpdateStatus();
    }

    public string Query
    {
        get =>
            (string)GetValue(
                QueryProperty);

        set =>
            SetValue(
                QueryProperty,
                value);
    }

    public IEnumerable? Suggestions
    {
        get =>
            GetValue(
                SuggestionsProperty)
                as IEnumerable;

        set =>
            SetValue(
                SuggestionsProperty,
                value);
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

    public bool CanLoadMore
    {
        get =>
            (bool)GetValue(
                CanLoadMoreProperty);

        set =>
            SetValue(
                CanLoadMoreProperty,
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

    public ICommand? SearchCommand
    {
        get =>
            GetCommand(
                SearchCommandProperty);

        set =>
            SetValue(
                SearchCommandProperty,
                value);
    }

    public ICommand? DebouncedSearchCommand
    {
        get =>
            GetCommand(
                DebouncedSearchCommandProperty);

        set =>
            SetValue(
                DebouncedSearchCommandProperty,
                value);
    }

    public ICommand? RefreshSuggestionsCommand
    {
        get =>
            GetCommand(
                RefreshSuggestionsCommandProperty);

        set =>
            SetValue(
                RefreshSuggestionsCommandProperty,
                value);
    }

    public ICommand? ClearQueryCommand
    {
        get =>
            GetCommand(
                ClearQueryCommandProperty);

        set =>
            SetValue(
                ClearQueryCommandProperty,
                value);
    }

    public ICommand? CancelCommand
    {
        get =>
            GetCommand(
                CancelCommandProperty);

        set =>
            SetValue(
                CancelCommandProperty,
                value);
    }

    public ICommand? LoadMoreCommand
    {
        get =>
            GetCommand(
                LoadMoreCommandProperty);

        set =>
            SetValue(
                LoadMoreCommandProperty,
                value);
    }

    public void FocusSearchBox()
    {
        PageSearchHeader.FocusSearchBox();
    }

    private static DependencyProperty RegisterCommand(
        string propertyName)
    {
        return DependencyProperty.Register(
            propertyName,
            typeof(ICommand),
            typeof(SearchPage),
            new PropertyMetadata(null));
    }

    private static void HandleVisualStateChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        ((SearchPage)sender)
            .UpdateVisualState();
    }

    private static void HandleStatusChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs
            eventArgs)
    {
        _ = eventArgs;

        ((SearchPage)sender)
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

        ResultsGridView.Visibility =
            showEmptyState
                ? Visibility.Collapsed
                : Visibility.Visible;

        LoadingTextBlock.Visibility =
            IsBusy
                ? Visibility.Visible
                : Visibility.Collapsed;

        LoadingProgressRing.Visibility =
            IsBusy
                ? Visibility.Visible
                : Visibility.Collapsed;

        LoadMoreButton.Visibility =
            CanLoadMore &&
            !IsBusy
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void UpdateStatus()
    {
        PageStatusBanner.IsOpen =
            !string.IsNullOrWhiteSpace(
                StatusMessage);
    }
}
