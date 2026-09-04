using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Library;
using CopyGIF.Presentation.Search;

namespace CopyGIF.Presentation.Main;

public enum MainSection
{
    Search,
    Favorites,
    Recents
}

public sealed class MainViewModel :
    ObservableObject,
    IDisposable
{
    private MainSection _selectedSection =
        MainSection.Search;

    private bool _reducedMotion;

    private bool _disposed;

    public MainViewModel(
        SearchViewModel search,
        FavoritesViewModel favorites,
        RecentsViewModel recents)
    {
        Search =
            search ??
            throw new ArgumentNullException(
                nameof(search));

        Favorites =
            favorites ??
            throw new ArgumentNullException(
                nameof(favorites));

        Recents =
            recents ??
            throw new ArgumentNullException(
                nameof(recents));

        SubscribeToChildren();

        ShowSearchCommand =
            new RelayCommand(
                ShowSearch,
                CanNavigate);

        ShowFavoritesCommand =
            new AsyncRelayCommand(
                ShowFavoritesAsync,
                CanNavigate);

        ShowRecentsCommand =
            new AsyncRelayCommand(
                ShowRecentsAsync,
                CanNavigate);

        RefreshCurrentSectionCommand =
            new AsyncRelayCommand(
                RefreshCurrentSectionAsync,
                CanRefresh);

        CancelActiveCommand =
            new RelayCommand(
                CancelActiveOperation,
                CanCancelActive);
    }

    public SearchViewModel Search
    { get; }

    public FavoritesViewModel Favorites
    { get; }

    public RecentsViewModel Recents
    { get; }

    public IRelayCommand ShowSearchCommand
    { get; }

    public IAsyncRelayCommand ShowFavoritesCommand
    { get; }

    public IAsyncRelayCommand ShowRecentsCommand
    { get; }

    public IAsyncRelayCommand RefreshCurrentSectionCommand
    { get; }

    public IRelayCommand CancelActiveCommand
    { get; }

    public MainSection SelectedSection
    {
        get => _selectedSection;

        private set
        {
            if (SetProperty(
                    ref _selectedSection,
                    value))
            {
                OnPropertyChanged(
                    nameof(IsSearchSelected));

                OnPropertyChanged(
                    nameof(IsFavoritesSelected));

                OnPropertyChanged(
                    nameof(IsRecentsSelected));

                OnPropertyChanged(
                    nameof(ActiveOperationState));

                OnPropertyChanged(
                    nameof(ActiveMessage));

                OnPropertyChanged(
                    nameof(IsBusy));

                NotifyCommandStates();
            }
        }
    }

    public bool IsSearchSelected =>
        SelectedSection ==
        MainSection.Search;

    public bool IsFavoritesSelected =>
        SelectedSection ==
        MainSection.Favorites;

    public bool IsRecentsSelected =>
        SelectedSection ==
        MainSection.Recents;

    public AsyncOperationState ActiveOperationState =>
        SelectedSection switch
        {
            MainSection.Search =>
                Search.OperationState,

            MainSection.Favorites =>
                Favorites.OperationState,

            MainSection.Recents =>
                Recents.OperationState,

            _ =>
                AsyncOperationState.Idle
        };

    public UserMessage? ActiveMessage =>
        SelectedSection switch
        {
            MainSection.Search =>
                Search.Message,

            MainSection.Favorites =>
                Favorites.Message,

            MainSection.Recents =>
                Recents.Message,

            _ =>
                null
        };

    public bool IsBusy =>
        ActiveOperationState.IsBusy;

    public bool IsAnyBusy =>
        Search.IsBusy ||
        Favorites.IsBusy ||
        Recents.IsBusy;

    public bool ReducedMotion
    {
        get => _reducedMotion;

        set
        {
            if (!SetProperty(
                    ref _reducedMotion,
                    value))
            {
                return;
            }

            Search.ReducedMotion =
                value;

            Favorites.ReducedMotion =
                value;

            Recents.ReducedMotion =
                value;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        UnsubscribeFromChildren();
    }

    private bool CanNavigate()
    {
        return !IsAnyBusy;
    }

    private bool CanRefresh()
    {
        return !IsAnyBusy;
    }

    private bool CanCancelActive()
    {
        return SelectedSection switch
        {
            MainSection.Search =>
                Search.CancelCommand
                    .CanExecute(null),

            MainSection.Favorites =>
                Favorites.CancelCommand
                    .CanExecute(null),

            MainSection.Recents =>
                Recents.CancelCommand
                    .CanExecute(null),

            _ =>
                false
        };
    }

    private void ShowSearch()
    {
        SelectedSection =
            MainSection.Search;
    }

    private async Task ShowFavoritesAsync()
    {
        SelectedSection =
            MainSection.Favorites;

        await Favorites
            .LoadCommand
            .ExecuteAsync(null);
    }

    private async Task ShowRecentsAsync()
    {
        SelectedSection =
            MainSection.Recents;

        await Recents
            .LoadCommand
            .ExecuteAsync(null);
    }

    private async Task RefreshCurrentSectionAsync()
    {
        switch (SelectedSection)
        {
            case MainSection.Search:
                if (string.IsNullOrWhiteSpace(
                        Search.Query))
                {
                    await Search
                        .TrendingCommand
                        .ExecuteAsync(null);
                }
                else
                {
                    await Search
                        .SearchCommand
                        .ExecuteAsync(null);
                }

                break;

            case MainSection.Favorites:
                await Favorites
                    .LoadCommand
                    .ExecuteAsync(null);

                break;

            case MainSection.Recents:
                await Recents
                    .LoadCommand
                    .ExecuteAsync(null);

                break;

            default:
                throw new InvalidOperationException(
                    "The selected main section is not supported.");
        }
    }

    private void CancelActiveOperation()
    {
        IRelayCommand command =
            SelectedSection switch
            {
                MainSection.Search =>
                    Search.CancelCommand,

                MainSection.Favorites =>
                    Favorites.CancelCommand,

                MainSection.Recents =>
                    Recents.CancelCommand,

                _ =>
                    throw new InvalidOperationException(
                        "The selected main section is not supported.")
            };

        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void SubscribeToChildren()
    {
        Search.PropertyChanged +=
            HandleChildPropertyChanged;

        Favorites.PropertyChanged +=
            HandleChildPropertyChanged;

        Recents.PropertyChanged +=
            HandleChildPropertyChanged;
    }

    private void UnsubscribeFromChildren()
    {
        Search.PropertyChanged -=
            HandleChildPropertyChanged;

        Favorites.PropertyChanged -=
            HandleChildPropertyChanged;

        Recents.PropertyChanged -=
            HandleChildPropertyChanged;
    }

    private void HandleChildPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is
            nameof(SearchViewModel.IsBusy) or
            nameof(SearchViewModel.OperationState) or
            nameof(SearchViewModel.Message))
        {
            OnPropertyChanged(
                nameof(IsAnyBusy));

            OnPropertyChanged(
                nameof(IsBusy));

            OnPropertyChanged(
                nameof(ActiveOperationState));

            OnPropertyChanged(
                nameof(ActiveMessage));

            NotifyCommandStates();
        }
    }

    private void NotifyCommandStates()
    {
        ShowSearchCommand
            .NotifyCanExecuteChanged();

        ShowFavoritesCommand
            .NotifyCanExecuteChanged();

        ShowRecentsCommand
            .NotifyCanExecuteChanged();

        RefreshCurrentSectionCommand
            .NotifyCanExecuteChanged();

        CancelActiveCommand
            .NotifyCanExecuteChanged();
    }
}
