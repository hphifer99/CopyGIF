using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CopyGIF.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CopyGIF.App.Views;

public sealed partial class OnboardingWindow :
    Window,
    INotifyPropertyChanged
{
    private string _stepTitle =
        "Welcome to CopyGIF";

    private string _stepDescription =
        "Complete the initial setup to start finding and copying GIFs.";

    private object? _stepContent;

    private DataTemplate? _stepContentTemplate;

    private bool _isBusy;

    private bool _canFinish;

    private string _statusMessage =
        string.Empty;

    private InfoBarSeverity _statusSeverity =
        InfoBarSeverity.Informational;

    private ICommand? _finishCommand;

    public OnboardingWindow()
    {
        InitializeComponent();

        WindowRoot.DataContext =
            this;

        WindowTitleBar.Apply(
            this,
            WindowRoot);

        UpdateVisualState();
        UpdateStatus();
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    public FrameworkElement RootElement =>
        WindowRoot;

    public string StepTitle
    {
        get =>
            _stepTitle;

        set =>
            SetProperty(
                ref _stepTitle,
                value ?? string.Empty);
    }

    public string StepDescription
    {
        get =>
            _stepDescription;

        set =>
            SetProperty(
                ref _stepDescription,
                value ?? string.Empty);
    }

    public object? StepContent
    {
        get =>
            _stepContent;

        set =>
            SetProperty(
                ref _stepContent,
                value);
    }

    public DataTemplate? StepContentTemplate
    {
        get =>
            _stepContentTemplate;

        set =>
            SetProperty(
                ref _stepContentTemplate,
                value);
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

    public bool CanFinish
    {
        get =>
            _canFinish;

        set
        {
            if (SetProperty(
                    ref _canFinish,
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

    public ICommand? FinishCommand
    {
        get =>
            _finishCommand;

        set =>
            SetProperty(
                ref _finishCommand,
                value);
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

    private void UpdateVisualState()
    {
        FinishButton.IsEnabled =
            CanFinish &&
            !IsBusy;
    }

    private void UpdateStatus()
    {
        PageStatusBanner.IsOpen =
            !string.IsNullOrWhiteSpace(
                StatusMessage);
    }
}
