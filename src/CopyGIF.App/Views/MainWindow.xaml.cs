using CopyGIF.App.Views.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace CopyGIF.App.Views;

public enum MainNavigationDestination
{
    Search,
    Favorites,
    Recents
}

public sealed partial class MainWindow :
    Window
{
    private bool _restoringSelection;

    public MainWindow()
    {
        InitializeComponent();

        SearchPage =
            new SearchPage();

        FavoritesPage =
            new FavoritesPage();

        RecentsPage =
            new RecentsPage();

        CurrentDestination =
            MainNavigationDestination.Search;

        SearchNavigationItem.IsSelected =
            true;

        ShowCurrentDestination();
    }

    public event EventHandler?
        SettingsRequested;

    public SearchPage SearchPage
    {
        get;
    }

    public FavoritesPage FavoritesPage
    {
        get;
    }

    public RecentsPage RecentsPage
    {
        get;
    }

    public FrameworkElement RootElement =>
        WindowRoot;

    public MainNavigationDestination CurrentDestination
    {
        get;
        private set;
    }

    public void Navigate(
        MainNavigationDestination destination)
    {
        CurrentDestination =
            destination;

        _restoringSelection =
            true;

        ShellNavigationView.SelectedItem =
            GetNavigationItem(
                destination);

        _restoringSelection =
            false;

        ShowCurrentDestination();
    }

    public void FocusSearch()
    {
        Navigate(
            MainNavigationDestination.Search);

        SearchPage.FocusSearchBox();
    }

    private void ShellNavigationView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs
            eventArgs)
    {
        _ = sender;

        if (_restoringSelection)
        {
            return;
        }

        if (eventArgs.IsSettingsSelected)
        {
            SettingsRequested?.Invoke(
                this,
                EventArgs.Empty);

            RestoreCurrentSelection();

            return;
        }

        if (eventArgs.SelectedItem is not
            NavigationViewItem selectedItem)
        {
            return;
        }

        if (ReferenceEquals(
                selectedItem,
                SearchNavigationItem))
        {
            CurrentDestination =
                MainNavigationDestination.Search;
        }
        else if (ReferenceEquals(
                     selectedItem,
                     FavoritesNavigationItem))
        {
            CurrentDestination =
                MainNavigationDestination.Favorites;
        }
        else if (ReferenceEquals(
                     selectedItem,
                     RecentsNavigationItem))
        {
            CurrentDestination =
                MainNavigationDestination.Recents;
        }
        else
        {
            return;
        }

        ShowCurrentDestination();
    }

    private void FocusSearchKeyboardAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs
            eventArgs)
    {
        _ = sender;

        FocusSearch();

        eventArgs.Handled =
            true;
    }

    private NavigationViewItem GetNavigationItem(
        MainNavigationDestination destination)
    {
        return destination switch
        {
            MainNavigationDestination.Search =>
                SearchNavigationItem,
            MainNavigationDestination.Favorites =>
                FavoritesNavigationItem,
            MainNavigationDestination.Recents =>
                RecentsNavigationItem,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(destination),
                    destination,
                    "The navigation destination is not supported.")
        };
    }

    private void RestoreCurrentSelection()
    {
        _restoringSelection =
            true;

        ShellNavigationView.SelectedItem =
            GetNavigationItem(
                CurrentDestination);

        _restoringSelection =
            false;
    }

    private void ShowCurrentDestination()
    {
        ShellNavigationView.Content =
            CurrentDestination switch
            {
                MainNavigationDestination.Search =>
                    SearchPage,
                MainNavigationDestination.Favorites =>
                    FavoritesPage,
                MainNavigationDestination.Recents =>
                    RecentsPage,
                _ =>
                    throw new InvalidOperationException(
                        "The current navigation destination is not supported.")
            };
    }
}
