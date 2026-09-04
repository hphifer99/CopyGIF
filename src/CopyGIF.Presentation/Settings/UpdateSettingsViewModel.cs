using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Settings;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;

namespace CopyGIF.Presentation.Settings;

public sealed class UpdateSettingsViewModel :
    ObservableObject,
    IDisposable
{
    private readonly ISettingsCoordinator
        _settingsCoordinator;

    private CancellationTokenSource?
        _operationCancellation;

    private bool _checkForUpdates =
        true;

    private UpdateCheckFrequency
        _checkFrequency =
            UpdateCheckFrequency.Daily;

    private UpdateMode _mode =
        UpdateMode.Recommended;

    private bool _isLoaded;

    private AsyncOperationState
        _operationState =
            AsyncOperationState.Idle;

    private UserMessage? _message;

    private bool _disposed;

    public UpdateSettingsViewModel(
        ISettingsCoordinator settingsCoordinator)
    {
        _settingsCoordinator =
            settingsCoordinator ??
            throw new ArgumentNullException(
                nameof(settingsCoordinator));

        LoadCommand =
            new AsyncRelayCommand(
                LoadAsync,
                CanStartOperation);

        SaveCommand =
            new AsyncRelayCommand(
                SaveAsync,
                CanSave);

        CancelCommand =
            new RelayCommand(
                CancelOperation,
                CanCancel);
    }

    public IAsyncRelayCommand LoadCommand
    { get; }

    public IAsyncRelayCommand SaveCommand
    { get; }

    public IRelayCommand CancelCommand
    { get; }

    public IReadOnlyList<UpdateCheckFrequency>
        CheckFrequencies
    { get; } =
        Enum.GetValues<
            UpdateCheckFrequency>();

    public IReadOnlyList<UpdateMode>
        Modes
    { get; } =
        Enum.GetValues<UpdateMode>();

    public bool CheckForUpdates
    {
        get => _checkForUpdates;

        set =>
            SetProperty(
                ref _checkForUpdates,
                value);
    }

    public UpdateCheckFrequency CheckFrequency
    {
        get => _checkFrequency;

        set
        {
            if (SetProperty(
                    ref _checkFrequency,
                    value))
            {
                NotifyValidationChanged();
            }
        }
    }

    public UpdateMode Mode
    {
        get => _mode;

        set
        {
            if (SetProperty(
                    ref _mode,
                    value))
            {
                NotifyValidationChanged();
            }
        }
    }

    public bool IsValid =>
        Enum.IsDefined(
            CheckFrequency) &&
        Enum.IsDefined(
            Mode);

    public bool IsLoaded
    {
        get => _isLoaded;

        private set
        {
            if (SetProperty(
                    ref _isLoaded,
                    value))
            {
                SaveCommand
                    .NotifyCanExecuteChanged();
            }
        }
    }

    public AsyncOperationState OperationState
    {
        get => _operationState;

        private set
        {
            if (SetProperty(
                    ref _operationState,
                    value))
            {
                OnPropertyChanged(
                    nameof(IsBusy));

                NotifyCommandStates();
            }
        }
    }

    public bool IsBusy =>
        OperationState.IsBusy;

    public UserMessage? Message
    {
        get => _message;

        private set =>
            SetProperty(
                ref _message,
                value);
    }

    public void ClearMessage()
    {
        Message =
            null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();

        _operationCancellation =
            null;
    }

    private bool CanStartOperation()
    {
        return !IsBusy;
    }

    private bool CanSave()
    {
        return
            !IsBusy &&
            IsLoaded &&
            IsValid;
    }

    private bool CanCancel()
    {
        return IsBusy;
    }

    private async Task LoadAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        CancellationTokenSource operation =
            BeginOperation(
                "Loading update settings...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    operation.Token,
                    cancellationToken);

        try
        {
            AppSettings settings =
                await _settingsCoordinator
                    .LoadAsync(
                        linkedCancellation.Token);

            ApplySettings(
                settings.Updates);

            IsLoaded =
                true;

            OperationState =
                AsyncOperationState.Succeeded(
                    "Update settings loaded.");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Update settings load cancelled.");

            Message =
                UserMessage.Information(
                    "Loading update settings was cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to load update settings.");

            Message =
                UserMessage.Error(
                    "Unable to load update settings.",
                    "update_settings_load_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task SaveAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!IsValid)
        {
            return;
        }

        CancellationTokenSource operation =
            BeginOperation(
                "Saving update settings...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    operation.Token,
                    cancellationToken);

        try
        {
            AppSettings current =
                await _settingsCoordinator
                    .LoadAsync(
                        linkedCancellation.Token);

            AppSettings requested =
                current with
                {
                    Updates =
                        current.Updates with
                        {
                            CheckForUpdates =
                                CheckForUpdates,

                            CheckFrequency =
                                CheckFrequency,

                            Mode =
                                Mode
                        }
                };

            SettingsSaveResult result =
                await _settingsCoordinator
                    .SaveAsync(
                        requested,
                        linkedCancellation.Token);

            ApplySettings(
                result.EffectiveSettings
                    .Updates);

            IsLoaded =
                true;

            if (result.Succeeded)
            {
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Update settings saved.");

                Message =
                    UserMessage.Success(
                        "Update settings saved.");

                return;
            }

            string message =
                string.IsNullOrWhiteSpace(
                    result.ErrorMessage)
                    ? "Unable to save update settings."
                    : result.ErrorMessage.Trim();

            OperationState =
                AsyncOperationState.Failed(
                    message);

            Message =
                UserMessage.Error(
                    message,
                    "update_settings_save_failed");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Update settings save cancelled.");

            Message =
                UserMessage.Information(
                    "Saving update settings was cancelled.");
        }
        catch (Exception exception)
            when (exception is
                ArgumentException or
                InvalidOperationException)
        {
            OperationState =
                AsyncOperationState.Failed(
                    exception.Message);

            Message =
                UserMessage.Warning(
                    exception.Message,
                    "update_settings_invalid");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to save update settings.");

            Message =
                UserMessage.Error(
                    "Unable to save update settings.",
                    "update_settings_save_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private void ApplySettings(
        Core.Settings.UpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        CheckForUpdates =
            settings.CheckForUpdates;

        CheckFrequency =
            settings.CheckFrequency;

        Mode =
            settings.Mode;
    }

    private void NotifyValidationChanged()
    {
        OnPropertyChanged(
            nameof(IsValid));

        SaveCommand
            .NotifyCanExecuteChanged();
    }

    private CancellationTokenSource BeginOperation(
        string message)
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();

        CancellationTokenSource cancellation =
            new();

        _operationCancellation =
            cancellation;

        Message =
            null;

        OperationState =
            AsyncOperationState.Running(
                message);

        return cancellation;
    }

    private void EndOperation(
        CancellationTokenSource cancellation)
    {
        if (ReferenceEquals(
                _operationCancellation,
                cancellation))
        {
            _operationCancellation =
                null;
        }

        cancellation.Dispose();

        NotifyCommandStates();
    }

    private void CancelOperation()
    {
        _operationCancellation?.Cancel();
    }

    private void NotifyCommandStates()
    {
        LoadCommand
            .NotifyCanExecuteChanged();

        SaveCommand
            .NotifyCanExecuteChanged();

        CancelCommand
            .NotifyCanExecuteChanged();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}
