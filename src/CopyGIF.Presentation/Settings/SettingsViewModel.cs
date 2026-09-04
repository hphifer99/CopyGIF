using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Settings;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Common;

namespace CopyGIF.Presentation.Settings;

public enum SettingsSection
{
    General,
    Search,
    Library,
    Appearance,
    Api,
    Updates
}

public sealed class SettingsViewModel :
    ObservableObject,
    IDisposable
{
    private readonly ISettingsCoordinator
        _settingsCoordinator;

    private CancellationTokenSource?
        _operationCancellation;

    private SettingsSection _selectedSection =
        SettingsSection.General;

    private bool _isLoaded;

    private AsyncOperationState _operationState =
        AsyncOperationState.Idle;

    private UserMessage? _message;

    private bool _disposed;

    public SettingsViewModel(
        ISettingsCoordinator settingsCoordinator,
        GeneralSettingsViewModel general,
        SearchSettingsViewModel search,
        LibrarySettingsViewModel library,
        AppearanceSettingsViewModel appearance,
        ApiSettingsViewModel api,
        UpdateSettingsViewModel updates)
    {
        _settingsCoordinator =
            settingsCoordinator ??
            throw new ArgumentNullException(
                nameof(settingsCoordinator));

        General =
            general ??
            throw new ArgumentNullException(
                nameof(general));

        Search =
            search ??
            throw new ArgumentNullException(
                nameof(search));

        Library =
            library ??
            throw new ArgumentNullException(
                nameof(library));

        Appearance =
            appearance ??
            throw new ArgumentNullException(
                nameof(appearance));

        Api =
            api ??
            throw new ArgumentNullException(
                nameof(api));

        Updates =
            updates ??
            throw new ArgumentNullException(
                nameof(updates));

        SubscribeToSections();

        LoadCommand =
            new AsyncRelayCommand(
                LoadAsync,
                CanStartOperation);

        RestoreDefaultsCommand =
            new AsyncRelayCommand(
                RestoreDefaultsAsync,
                CanStartOperation);

        CancelCommand =
            new RelayCommand(
                CancelOperations,
                CanCancel);
    }

    public GeneralSettingsViewModel General
    { get; }

    public SearchSettingsViewModel Search
    { get; }

    public LibrarySettingsViewModel Library
    { get; }

    public AppearanceSettingsViewModel Appearance
    { get; }

    public ApiSettingsViewModel Api
    { get; }

    public UpdateSettingsViewModel Updates
    { get; }

    public IAsyncRelayCommand LoadCommand
    { get; }

    public IAsyncRelayCommand RestoreDefaultsCommand
    { get; }

    public IRelayCommand CancelCommand
    { get; }

    public SettingsSection SelectedSection
    {
        get => _selectedSection;

        set =>
            SetProperty(
                ref _selectedSection,
                value);
    }

    public bool IsLoaded
    {
        get => _isLoaded;

        private set =>
            SetProperty(
                ref _isLoaded,
                value);
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

                OnPropertyChanged(
                    nameof(IsBusyOverall));

                NotifyCommandStates();
            }
        }
    }

    public bool IsBusy =>
        OperationState.IsBusy;

    public bool IsAnySectionBusy =>
        General.IsBusy ||
        Search.IsBusy ||
        Library.IsBusy ||
        Appearance.IsBusy ||
        Api.IsBusy ||
        Updates.IsBusy;

    public bool IsBusyOverall =>
        IsBusy ||
        IsAnySectionBusy;

    public bool HasSectionErrors =>
        General.OperationState.HasError ||
        Search.OperationState.HasError ||
        Library.OperationState.HasError ||
        Appearance.OperationState.HasError ||
        Api.OperationState.HasError ||
        Updates.OperationState.HasError;

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

        UnsubscribeFromSections();
    }

    private bool CanStartOperation()
    {
        return !IsBusyOverall;
    }

    private bool CanCancel()
    {
        return IsBusyOverall;
    }

    private async Task LoadAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        CancellationTokenSource operation =
            BeginOperation(
                "Loading settings...",
                cancellationToken);

        try
        {
            await LoadAllSectionsAsync();

            ApplyAggregateLoadState(
                "Settings loaded.");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Settings load cancelled.");

            Message =
                UserMessage.Information(
                    "Loading settings was cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to load settings.");

            Message =
                UserMessage.Error(
                    "Unable to load settings.",
                    "settings_load_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task RestoreDefaultsAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        CancellationTokenSource operation =
            BeginOperation(
                "Restoring default settings...",
                cancellationToken);

        try
        {
            SettingsSaveResult result =
                await _settingsCoordinator
                    .RestoreDefaultsAsync(
                        operation.Token);

            await LoadAllSectionsAsync();

            IsLoaded =
                AreAllSectionsLoaded();

            if (HasSectionErrors)
            {
                OperationState =
                    AsyncOperationState.Failed(
                        "Defaults were processed, but the settings screen could not be refreshed.");

                Message =
                    GetFirstSectionMessage() ??
                    UserMessage.Error(
                        "Unable to refresh settings after restoring defaults.",
                        "settings_refresh_failed");

                return;
            }

            if (result.Succeeded)
            {
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Default settings restored.");

                Message =
                    UserMessage.Success(
                        "Default settings restored.");

                return;
            }

            ApplyRestoreFailure(
                result);
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Restore defaults cancelled.");

            Message =
                UserMessage.Information(
                    "Restoring default settings was cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to restore default settings.");

            Message =
                UserMessage.Error(
                    "Unable to restore default settings.",
                    "restore_defaults_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task LoadAllSectionsAsync()
    {
        Task general =
            General.LoadCommand
                .ExecuteAsync(null);

        Task search =
            Search.LoadCommand
                .ExecuteAsync(null);

        Task library =
            Library.LoadCommand
                .ExecuteAsync(null);

        Task appearance =
            Appearance.LoadCommand
                .ExecuteAsync(null);

        Task api =
            Api.LoadCommand
                .ExecuteAsync(null);

        Task updates =
            Updates.LoadCommand
                .ExecuteAsync(null);

        await Task.WhenAll(
            general,
            search,
            library,
            appearance,
            api,
            updates);
    }

    private void ApplyAggregateLoadState(
        string successMessage)
    {
        IsLoaded =
            AreAllSectionsLoaded();

        if (HasSectionErrors)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "One or more settings sections could not be loaded.");

            Message =
                GetFirstSectionMessage() ??
                UserMessage.Error(
                    "One or more settings sections could not be loaded.",
                    "settings_section_load_failed");

            return;
        }

        if (HasSectionCancellation())
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Settings load cancelled.");

            Message =
                UserMessage.Information(
                    "Loading settings was cancelled.");

            return;
        }

        OperationState =
            AsyncOperationState.Succeeded(
                successMessage);

        Message =
            null;
    }

    private void ApplyRestoreFailure(
        SettingsSaveResult result)
    {
        string message =
            !string.IsNullOrWhiteSpace(
                result.ErrorMessage)
                ? result.ErrorMessage.Trim()
                : result.HotkeyFailure switch
                {
                    HotkeyRegistrationFailure.InvalidGesture =>
                        "The default hotkey could not be registered because it is invalid.",

                    HotkeyRegistrationFailure.Conflict =>
                        "The default hotkey is already in use.",

                    HotkeyRegistrationFailure.SystemRejected =>
                        "Windows rejected the default hotkey.",

                    _ =>
                        "Unable to restore default settings."
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
                        "default_hotkey_invalid"),

                HotkeyRegistrationFailure.Conflict =>
                    UserMessage.Warning(
                        message,
                        "default_hotkey_conflict"),

                HotkeyRegistrationFailure.SystemRejected =>
                    UserMessage.Warning(
                        message,
                        "default_hotkey_rejected"),

                _ =>
                    UserMessage.Error(
                        message,
                        "restore_defaults_failed")
            };
    }

    private bool AreAllSectionsLoaded()
    {
        return
            General.IsLoaded &&
            Search.IsLoaded &&
            Library.IsLoaded &&
            Appearance.IsLoaded &&
            Api.IsLoaded &&
            Updates.IsLoaded;
    }

    private bool HasSectionCancellation()
    {
        return
            General.OperationState.IsCancelled ||
            Search.OperationState.IsCancelled ||
            Library.OperationState.IsCancelled ||
            Appearance.OperationState.IsCancelled ||
            Api.OperationState.IsCancelled ||
            Updates.OperationState.IsCancelled;
    }

    private UserMessage? GetFirstSectionMessage()
    {
        return
            General.Message ??
            Search.Message ??
            Library.Message ??
            Appearance.Message ??
            Api.Message ??
            Updates.Message;
    }

    private CancellationTokenSource BeginOperation(
        string message,
        CancellationToken cancellationToken)
    {
        _operationCancellation?.Cancel();
        _operationCancellation?.Dispose();

        CancellationTokenSource operation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken);

        _operationCancellation =
            operation;

        Message =
            null;

        OperationState =
            AsyncOperationState.Running(
                message);

        return operation;
    }

    private void EndOperation(
        CancellationTokenSource operation)
    {
        if (ReferenceEquals(
                _operationCancellation,
                operation))
        {
            _operationCancellation =
                null;
        }

        operation.Dispose();

        NotifyAggregateState();
    }

    private void CancelOperations()
    {
        _operationCancellation?.Cancel();

        CancelSection(
            General.CancelCommand);

        CancelSection(
            Search.CancelCommand);

        CancelSection(
            Library.CancelCommand);

        CancelSection(
            Appearance.CancelCommand);

        CancelSection(
            Api.CancelCommand);

        CancelSection(
            Updates.CancelCommand);
    }

    private static void CancelSection(
        IRelayCommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void SubscribeToSections()
    {
        General.PropertyChanged +=
            HandleSectionPropertyChanged;

        Search.PropertyChanged +=
            HandleSectionPropertyChanged;

        Library.PropertyChanged +=
            HandleSectionPropertyChanged;

        Appearance.PropertyChanged +=
            HandleSectionPropertyChanged;

        Api.PropertyChanged +=
            HandleSectionPropertyChanged;

        Updates.PropertyChanged +=
            HandleSectionPropertyChanged;
    }

    private void UnsubscribeFromSections()
    {
        General.PropertyChanged -=
            HandleSectionPropertyChanged;

        Search.PropertyChanged -=
            HandleSectionPropertyChanged;

        Library.PropertyChanged -=
            HandleSectionPropertyChanged;

        Appearance.PropertyChanged -=
            HandleSectionPropertyChanged;

        Api.PropertyChanged -=
            HandleSectionPropertyChanged;

        Updates.PropertyChanged -=
            HandleSectionPropertyChanged;
    }

    private void HandleSectionPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is
            nameof(GeneralSettingsViewModel.IsBusy) or
            nameof(GeneralSettingsViewModel.OperationState) or
            nameof(GeneralSettingsViewModel.Message) or
            nameof(GeneralSettingsViewModel.IsLoaded))
        {
            NotifyAggregateState();
        }
    }

    private void NotifyAggregateState()
    {
        OnPropertyChanged(
            nameof(IsAnySectionBusy));

        OnPropertyChanged(
            nameof(IsBusyOverall));

        OnPropertyChanged(
            nameof(HasSectionErrors));

        NotifyCommandStates();
    }

    private void NotifyCommandStates()
    {
        LoadCommand
            .NotifyCanExecuteChanged();

        RestoreDefaultsCommand
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
