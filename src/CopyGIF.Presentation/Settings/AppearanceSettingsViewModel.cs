using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Settings;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;

namespace CopyGIF.Presentation.Settings;

public sealed class AppearanceSettingsViewModel :
    ObservableObject,
    IDisposable
{
    private readonly ISettingsCoordinator
        _settingsCoordinator;

    private CancellationTokenSource?
        _operationCancellation;

    private AppTheme _theme =
        AppTheme.System;

    private bool _isLoaded;

    private AsyncOperationState
        _operationState =
            AsyncOperationState.Idle;

    private UserMessage? _message;

    private bool _disposed;

    public AppearanceSettingsViewModel(
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

    public IReadOnlyList<AppTheme> Themes
    { get; } =
        Enum.GetValues<AppTheme>();

    public AppTheme Theme
    {
        get => _theme;

        set =>
            SetProperty(
                ref _theme,
                value);
    }

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
            IsLoaded;
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
                "Loading appearance settings...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                operation.Token,
                cancellationToken);

        try
        {
            AppSettings settings =
                await _settingsCoordinator
                    .LoadAsync(
                        linkedCancellation.Token);

            Theme =
                settings.Appearance.Theme;

            IsLoaded =
                true;

            OperationState =
                AsyncOperationState.Succeeded(
                    "Appearance settings loaded.");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Appearance settings load cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to load appearance settings.");

            Message =
                UserMessage.Error(
                    "Unable to load appearance settings.",
                    "appearance_settings_load_failed");
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

        CancellationTokenSource operation =
            BeginOperation(
                "Saving appearance settings...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
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
                    Appearance =
                        current.Appearance with
                        {
                            Theme =
                                Theme
                        }
                };

            SettingsSaveResult result =
                await _settingsCoordinator
                    .SaveAsync(
                        requested,
                        linkedCancellation.Token);

            Theme =
                result.EffectiveSettings
                    .Appearance
                    .Theme;

            IsLoaded =
                true;

            if (result.Succeeded)
            {
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Appearance settings saved.");

                Message =
                    UserMessage.Success(
                        "Appearance settings saved.");

                return;
            }

            string message =
                string.IsNullOrWhiteSpace(
                    result.ErrorMessage)
                    ? "Unable to save appearance settings."
                    : result.ErrorMessage.Trim();

            OperationState =
                AsyncOperationState.Failed(
                    message);

            Message =
                UserMessage.Error(
                    message,
                    "appearance_settings_save_failed");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Appearance settings save cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to save appearance settings.");

            Message =
                UserMessage.Error(
                    "Unable to save appearance settings.",
                    "appearance_settings_save_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
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
