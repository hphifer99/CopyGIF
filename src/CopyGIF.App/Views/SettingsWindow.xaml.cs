using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CopyGIF.App.Views;

public enum SettingsSection
{
    General,
    Search,
    Appearance,
    Library,
    Api,
    Updates
}

public sealed partial class SettingsWindow :
    Window,
    INotifyPropertyChanged
{
    private object? _generalContent;

    private object? _searchContent;

    private object? _appearanceContent;

    private object? _libraryContent;

    private object? _apiContent;

    private object? _updatesContent;

    private DataTemplate? _generalContentTemplate;

    private DataTemplate? _searchContentTemplate;

    private DataTemplate? _appearanceContentTemplate;

    private DataTemplate? _libraryContentTemplate;

    private DataTemplate? _apiContentTemplate;

    private DataTemplate? _updatesContentTemplate;

    private bool _isBusy;

    private bool _hasUnsavedChanges;

    private string _statusMessage =
        string.Empty;

    private InfoBarSeverity _statusSeverity =
        InfoBarSeverity.Informational;

    private ICommand? _saveCommand;

    private ICommand? _cancelCommand;

    public SettingsWindow()
    {
        InitializeComponent();

        WindowRoot.DataContext =
            this;

        CurrentSection =
            SettingsSection.General;

        GeneralNavigationItem.IsSelected =
            true;

        UpdateSection();
        UpdateVisualState();
        UpdateStatus();
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public FrameworkElement RootElement =>
        WindowRoot;

    public SettingsSection CurrentSection
    {
        get;
        private set;
    }

    public object? GeneralContent
    {
        get =>
            _generalContent;

        set
        {
            if (SetProperty(
                    ref _generalContent,
                    value) &&
                CurrentSection ==
                    SettingsSection.General)
            {
                UpdateSection();
            }
        }
    }

    public object? SearchContent
    {
        get =>
            _searchContent;

        set
        {
            if (SetProperty(
                    ref _searchContent,
                    value) &&
                CurrentSection ==
                    SettingsSection.Search)
            {
                UpdateSection();
            }
        }
    }

    public object? AppearanceContent
    {
        get =>
            _appearanceContent;

        set
        {
            if (SetProperty(
                    ref _appearanceContent,
                    value) &&
                CurrentSection ==
                    SettingsSection.Appearance)
            {
                UpdateSection();
            }
        }
    }

    public object? LibraryContent
    {
        get =>
            _libraryContent;

        set
        {
            if (SetProperty(
                    ref _libraryContent,
                    value) &&
                CurrentSection ==
                    SettingsSection.Library)
            {
                UpdateSection();
            }
        }
    }

    public object? ApiContent
    {
        get =>
            _apiContent;

        set
        {
            if (SetProperty(
                    ref _apiContent,
                    value) &&
                CurrentSection ==
                    SettingsSection.Api)
            {
                UpdateSection();
            }
        }
    }

    public object? UpdatesContent
    {
        get =>
            _updatesContent;

        set
        {
            if (SetProperty(
                    ref _updatesContent,
                    value) &&
                CurrentSection ==
                    SettingsSection.Updates)
            {
                UpdateSection();
            }
        }
    }

    public DataTemplate? GeneralContentTemplate
    {
        get =>
            _generalContentTemplate;

        set
        {
            if (SetProperty(
                    ref _generalContentTemplate,
                    value) &&
                CurrentSection ==
                    SettingsSection.General)
            {
                UpdateSection();
            }
        }
    }

    public DataTemplate? SearchContentTemplate
    {
        get =>
            _searchContentTemplate;

        set
        {
            if (SetProperty(
                    ref _searchContentTemplate,
                    value) &&
                CurrentSection ==
                    SettingsSection.Search)
            {
                UpdateSection();
            }
        }
    }

    public DataTemplate? AppearanceContentTemplate
    {
        get =>
            _appearanceContentTemplate;

        set
        {
            if (SetProperty(
                    ref _appearanceContentTemplate,
                    value) &&
                CurrentSection ==
                    SettingsSection.Appearance)
            {
                UpdateSection();
            }
        }
    }

    public DataTemplate? LibraryContentTemplate
    {
        get =>
            _libraryContentTemplate;

        set
        {
            if (SetProperty(
                    ref _libraryContentTemplate,
                    value) &&
                CurrentSection ==
                    SettingsSection.Library)
            {
                UpdateSection();
            }
        }
    }

    public DataTemplate? ApiContentTemplate
    {
        get =>
            _apiContentTemplate;

        set
        {
            if (SetProperty(
                    ref _apiContentTemplate,
                    value) &&
                CurrentSection ==
                    SettingsSection.Api)
            {
                UpdateSection();
            }
        }
    }

    public DataTemplate? UpdatesContentTemplate
    {
        get =>
            _updatesContentTemplate;

        set
        {
            if (SetProperty(
                    ref _updatesContentTemplate,
                    value) &&
                CurrentSection ==
                    SettingsSection.Updates)
            {
                UpdateSection();
            }
        }
    }

    public bool IsBusy
    {
        get =>
            _isBusy;

        set
        {
            if (SetProperty(
                    ref _isBusy,
                    value))
            {
                UpdateVisualState();
            }
        }
    }

    public bool HasUnsavedChanges
    {
        get =>
            _hasUnsavedChanges;

        set
        {
            if (SetProperty(
                    ref _hasUnsavedChanges,
                    value))
            {
                UpdateVisualState();
            }
        }
    }

    public string StatusMessage
    {
        get =>
            _statusMessage;

        set
        {
            if (SetProperty(
                    ref _statusMessage,
                    value ?? string.Empty))
            {
                UpdateStatus();
            }
        }
    }

    public InfoBarSeverity StatusSeverity
    {
        get =>
            _statusSeverity;

        set =>
            SetProperty(
                ref _statusSeverity,
                value);
    }

    public ICommand? SaveCommand
    {
        get =>
            _saveCommand;

        set =>
            SetProperty(
                ref _saveCommand,
                value);
    }

    public ICommand? CancelCommand
    {
        get =>
            _cancelCommand;

        set =>
            SetProperty(
                ref _cancelCommand,
                value);
    }

    public void Navigate(
        SettingsSection section)
    {
        if (!Enum.IsDefined(
                section))
        {
            throw new ArgumentOutOfRangeException(
                nameof(section),
                section,
                "The settings section is not supported.");
        }

        CurrentSection =
            section;

        SettingsNavigationView.SelectedItem =
            GetNavigationItem(
                section);

        UpdateSection();
    }

    private void SettingsNavigationView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs
            eventArgs)
    {
        _ = sender;

        if (eventArgs.SelectedItem is not
            NavigationViewItem selectedItem)
        {
            return;
        }

        if (ReferenceEquals(
                selectedItem,
                GeneralNavigationItem))
        {
            CurrentSection =
                SettingsSection.General;
        }
        else if (ReferenceEquals(
                     selectedItem,
                     SearchNavigationItem))
        {
            CurrentSection =
                SettingsSection.Search;
        }
        else if (ReferenceEquals(
                     selectedItem,
                     AppearanceNavigationItem))
        {
            CurrentSection =
                SettingsSection.Appearance;
        }
        else if (ReferenceEquals(
                     selectedItem,
                     LibraryNavigationItem))
        {
            CurrentSection =
                SettingsSection.Library;
        }
        else if (ReferenceEquals(
                     selectedItem,
                     ApiNavigationItem))
        {
            CurrentSection =
                SettingsSection.Api;
        }
        else if (ReferenceEquals(
                     selectedItem,
                     UpdatesNavigationItem))
        {
            CurrentSection =
                SettingsSection.Updates;
        }
        else
        {
            return;
        }

        UpdateSection();
    }

    private NavigationViewItem GetNavigationItem(
        SettingsSection section)
    {
        return section switch
        {
            SettingsSection.General =>
                GeneralNavigationItem,
            SettingsSection.Search =>
                SearchNavigationItem,
            SettingsSection.Appearance =>
                AppearanceNavigationItem,
            SettingsSection.Library =>
                LibraryNavigationItem,
            SettingsSection.Api =>
                ApiNavigationItem,
            SettingsSection.Updates =>
                UpdatesNavigationItem,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(section),
                    section,
                    "The settings section is not supported.")
        };
    }

    private object? GetSectionContent()
    {
        return CurrentSection switch
        {
            SettingsSection.General =>
                GeneralContent,
            SettingsSection.Search =>
                SearchContent,
            SettingsSection.Appearance =>
                AppearanceContent,
            SettingsSection.Library =>
                LibraryContent,
            SettingsSection.Api =>
                ApiContent,
            SettingsSection.Updates =>
                UpdatesContent,
            _ =>
                throw new InvalidOperationException(
                    "The current settings section is not supported.")
        };
    }

    private string GetSectionTitle()
    {
        return CurrentSection switch
        {
            SettingsSection.General =>
                "General",
            SettingsSection.Search =>
                "Search",
            SettingsSection.Appearance =>
                "Appearance",
            SettingsSection.Library =>
                "Library",
            SettingsSection.Api =>
                "API",
            SettingsSection.Updates =>
                "Updates",
            _ =>
                throw new InvalidOperationException(
                    "The current settings section is not supported.")
        };
    }

    private DataTemplate? GetSectionTemplate()
    {
        return CurrentSection switch
        {
            SettingsSection.General =>
                GeneralContentTemplate,
            SettingsSection.Search =>
                SearchContentTemplate,
            SettingsSection.Appearance =>
                AppearanceContentTemplate,
            SettingsSection.Library =>
                LibraryContentTemplate,
            SettingsSection.Api =>
                ApiContentTemplate,
            SettingsSection.Updates =>
                UpdatesContentTemplate,
            _ =>
                throw new InvalidOperationException(
                    "The current settings section is not supported.")
        };
    }

    private bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName]
        string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(
                field,
                value))
        {
            return false;
        }

        field =
            value;

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));

        return true;
    }

    private void UpdateSection()
    {
        SectionTitleTextBlock.Text =
            GetSectionTitle();

        SectionContentPresenter.Content =
            GetSectionContent();

        SectionContentPresenter.ContentTemplate =
            GetSectionTemplate();
    }

    private void UpdateVisualState()
    {
        SavingProgressRing.Visibility =
            IsBusy
                ? Visibility.Visible
                : Visibility.Collapsed;

        SettingsNavigationView.IsEnabled =
            !IsBusy;

        CancelButton.IsEnabled =
            !IsBusy;

        SaveButton.IsEnabled =
            HasUnsavedChanges &&
            !IsBusy;
    }

    private void UpdateStatus()
    {
        PageStatusBanner.IsOpen =
            !string.IsNullOrWhiteSpace(
                StatusMessage);
    }
}
