using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
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

    private int _currentStepNumber =
        1;

    private int _stepCount =
        1;

    private object? _stepContent;

    private DataTemplate? _stepContentTemplate;

    private bool _isBusy;

    private bool _canGoBack;

    private bool _isFinalStep;

    private string _statusMessage =
        string.Empty;

    private InfoBarSeverity _statusSeverity =
        InfoBarSeverity.Informational;

    private ICommand? _backCommand;

    private ICommand? _nextCommand;

    private ICommand? _finishCommand;

    private ICommand? _cancelCommand;

    public OnboardingWindow()
    {
        InitializeComponent();

        WindowRoot.DataContext =
            this;

        UpdateVisualState();
        UpdateStatus();
        UpdateProgressText();
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

    public int CurrentStepNumber
    {
        get =>
            _currentStepNumber;

        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The current step number cannot be negative.");
            }

            if (SetProperty(
                    ref _currentStepNumber,
                    value))
            {
                UpdateProgressText();
            }
        }
    }

    public int StepCount
    {
        get =>
            _stepCount;

        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The onboarding step count must be at least one.");
            }

            if (SetProperty(
                    ref _stepCount,
                    value))
            {
                UpdateProgressText();
            }
        }
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

    public bool CanGoBack
    {
        get =>
            _canGoBack;

        set
        {
            if (SetProperty(
                    ref _canGoBack,
                    value))
            {
                UpdateVisualState();
            }
        }
    }

    public bool IsFinalStep
    {
        get =>
            _isFinalStep;

        set
        {
            if (SetProperty(
                    ref _isFinalStep,
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

    public ICommand? BackCommand
    {
        get =>
            _backCommand;

        set =>
            SetProperty(
                ref _backCommand,
                value);
    }

    public ICommand? NextCommand
    {
        get =>
            _nextCommand;

        set =>
            SetProperty(
                ref _nextCommand,
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

    public ICommand? CancelCommand
    {
        get =>
            _cancelCommand;

        set =>
            SetProperty(
                ref _cancelCommand,
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
        BackButton.IsEnabled =
            CanGoBack &&
            !IsBusy;

        CancelButton.IsEnabled =
            !IsBusy;

        NextButton.IsEnabled =
            !IsBusy;

        FinishButton.IsEnabled =
            !IsBusy;

        NextButton.Visibility =
            IsFinalStep
                ? Visibility.Collapsed
                : Visibility.Visible;

        FinishButton.Visibility =
            IsFinalStep
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void UpdateStatus()
    {
        PageStatusBanner.IsOpen =
            !string.IsNullOrWhiteSpace(
                StatusMessage);
    }

    private void UpdateProgressText()
    {
        StepProgressTextBlock.Text =
            $"Step {CurrentStepNumber} of {StepCount}";
    }
}
