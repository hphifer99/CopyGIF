using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Settings;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;

namespace CopyGIF.Presentation.Settings;

public sealed class GeneralSettingsViewModel :
    ObservableObject,
    IDisposable
{
    private readonly ISettingsCoordinator
        _settingsCoordinator;

    private CancellationTokenSource?
        _operationCancellation;

    private string _hotkey =
        AppSettings.DefaultHotkey;

    private bool _startWithWindows =
        true;

    private bool _closeWhenFocusLost =
        true;

    private bool _hideAfterCopy =
        true;

    private WindowPlacementMode _placementMode =
        WindowPlacementMode.Mouse;

    private bool _rememberWindowSize =
        true;

    private bool _isLoaded;

    private AsyncOperationState
        _operationState =
            AsyncOperationState.Idle;

    private UserMessage? _message;

    private bool _disposed;

    public GeneralSettingsViewModel(
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

    public IReadOnlyList<WindowPlacementMode>
        PlacementModes
    { get; } =
        Enum.GetValues<WindowPlacementMode>();

    public string Hotkey
    {
        get => _hotkey;

        set
        {
            string normalized =
                value ??
                string.Empty;

            if (SetProperty(
                    ref _hotkey,
                    normalized))
            {
                OnPropertyChanged(
                    nameof(HasValidHotkeyInput));

                SaveCommand
                    .NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasValidHotkeyInput =>
        !string.IsNullOrWhiteSpace(
            Hotkey);

    public bool StartWithWindows
    {
        get => _startWithWindows;

        set =>
            SetProperty(
                ref _startWithWindows,
                value);
    }

    public bool CloseWhenFocusLost
    {
        get => _closeWhenFocusLost;

        set =>
            SetProperty(
                ref _closeWhenFocusLost,
                value);
    }

    public bool HideAfterCopy
    {
        get => _hideAfterCopy;

        set =>
            SetProperty(
                ref _hideAfterCopy,
                value);
    }

    public WindowPlacementMode PlacementMode
    {
        get => _placementMode;

        set =>
            SetProperty(
                ref _placementMode,
                value);
    }

    public bool RememberWindowSize
    {
        get => _rememberWindowSize;

        set =>
            SetProperty(
                ref _rememberWindowSize,
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
            IsLoaded &&
            HasValidHotkeyInput;
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
                "Loading general settings...");

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

            ApplySettings(
                settings);

            IsLoaded =
                true;

            OperationState =
                AsyncOperationState.Succeeded(
                    "General settings loaded.");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "General settings load cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to load general settings.");

            Message =
                UserMessage.Error(
                    "Unable to load general settings.",
                    "general_settings_load_failed");
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

        string hotkey =
            Hotkey.Trim();

        if (string.IsNullOrWhiteSpace(
                hotkey))
        {
            return;
        }

        CancellationTokenSource operation =
            BeginOperation(
                "Saving general settings...");

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
                    Hotkey =
                        hotkey,

                    Startup =
                        current.Startup with
                        {
                            StartWithWindows =
                                StartWithWindows
                        },

                    Behavior =
                        current.Behavior with
                        {
                            CloseWhenFocusLost =
                                CloseWhenFocusLost,

                            HideAfterCopy =
                                HideAfterCopy
                        },

                    Window =
                        current.Window with
                        {
                            PlacementMode =
                                PlacementMode,

                            RememberWindowSize =
                                RememberWindowSize
                        }
                };

            SettingsSaveResult result =
                await _settingsCoordinator
                    .SaveAsync(
                        requested,
                        linkedCancellation.Token);

            ApplySettings(
                result.EffectiveSettings);

            IsLoaded =
                true;

            if (result.Succeeded)
            {
                OperationState =
                    AsyncOperationState.Succeeded(
                        "General settings saved.");

                Message =
                    UserMessage.Success(
                        "General settings saved.");

                return;
            }

            ApplySaveFailure(
                result);
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "General settings save cancelled.");

            Message =
                UserMessage.Information(
                    "Saving general settings was cancelled.");
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
                    "general_settings_invalid");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to save general settings.");

            Message =
                UserMessage.Error(
                    "Unable to save general settings.",
                    "general_settings_save_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private void ApplySettings(
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        Hotkey =
            settings.Hotkey;

        StartWithWindows =
            settings.Startup.StartWithWindows;

        CloseWhenFocusLost =
            settings.Behavior.CloseWhenFocusLost;

        HideAfterCopy =
            settings.Behavior.HideAfterCopy;

        PlacementMode =
            settings.Window.PlacementMode;

        RememberWindowSize =
            settings.Window.RememberWindowSize;
    }

    private void ApplySaveFailure(
        SettingsSaveResult result)
    {
        string message =
            !string.IsNullOrWhiteSpace(
                result.ErrorMessage)
                ? result.ErrorMessage.Trim()
                : result.HotkeyFailure switch
                {
                    HotkeyRegistrationFailure.InvalidGesture =>
                        "The hotkey is not valid.",

                    HotkeyRegistrationFailure.Conflict =>
                        "The hotkey is already in use.",

                    HotkeyRegistrationFailure.SystemRejected =>
                        "Windows rejected the hotkey.",

                    _ =>
                        "Unable to save general settings."
                };

        OperationState =
            AsyncOperationState.Failed(
                message);

        Message =
            result.HotkeyFailure switch
            {
                HotkeyRegistrationFailure.InvalidGesture =>
                    UserMessage.Warning(
                        message,
                        "hotkey_invalid"),

                HotkeyRegistrationFailure.Conflict =>
                    UserMessage.Warning(
                        message,
                        "hotkey_conflict"),

                HotkeyRegistrationFailure.SystemRejected =>
                    UserMessage.Warning(
                        message,
                        "hotkey_rejected"),

                _ =>
                    UserMessage.Error(
                        message,
                        "general_settings_save_failed")
            };
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
