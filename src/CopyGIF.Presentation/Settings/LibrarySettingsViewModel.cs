using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Settings;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;

namespace CopyGIF.Presentation.Settings;

public sealed class LibrarySettingsViewModel :
    ObservableObject,
    IDisposable
{
    private readonly ISettingsCoordinator
        _settingsCoordinator;

    private CancellationTokenSource?
        _operationCancellation;

    private int _recentLimit =
        30;

    private int _favoriteLimit =
        100;

    private bool _storeFavoritesLocally =
        true;

    private bool _storeRecentsLocally =
        true;

    private string? _customStorageRoot;

    private bool _isLoaded;

    private AsyncOperationState
        _operationState =
            AsyncOperationState.Idle;

    private UserMessage? _message;

    private bool _disposed;

    public LibrarySettingsViewModel(
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

        ChooseStorageRootCommand =
            new AsyncRelayCommand(
                ChooseStorageRootAsync,
                CanStartOperation);

        ResetStorageRootCommand =
            new AsyncRelayCommand(
                ResetStorageRootAsync,
                CanResetStorageRoot);

        CancelCommand =
            new RelayCommand(
                CancelOperation,
                CanCancel);
    }

    public IAsyncRelayCommand LoadCommand
    { get; }

    public IAsyncRelayCommand SaveCommand
    { get; }

    public IAsyncRelayCommand ChooseStorageRootCommand
    { get; }

    public IAsyncRelayCommand ResetStorageRootCommand
    { get; }

    public IRelayCommand CancelCommand
    { get; }

    public int MinimumRecentLimit
    { get; } =
        AppSettingsValidator
            .MinimumRecentLimit;

    public int MaximumRecentLimit
    { get; } =
        AppSettingsValidator
            .MaximumRecentLimit;

    public int MinimumFavoriteLimit
    { get; } =
        AppSettingsValidator
            .MinimumFavoriteLimit;

    public int MaximumFavoriteLimit
    { get; } =
        AppSettingsValidator
            .MaximumFavoriteLimit;

    public int RecentLimit
    {
        get => _recentLimit;

        set
        {
            if (SetProperty(
                    ref _recentLimit,
                    value))
            {
                NotifyValidationChanged();
            }
        }
    }

    public int FavoriteLimit
    {
        get => _favoriteLimit;

        set
        {
            if (SetProperty(
                    ref _favoriteLimit,
                    value))
            {
                NotifyValidationChanged();
            }
        }
    }

    public bool StoreFavoritesLocally
    {
        get => _storeFavoritesLocally;

        set =>
            SetProperty(
                ref _storeFavoritesLocally,
                value);
    }

    public bool StoreRecentsLocally
    {
        get => _storeRecentsLocally;

        set =>
            SetProperty(
                ref _storeRecentsLocally,
                value);
    }

    public string? CustomStorageRoot
    {
        get => _customStorageRoot;

        private set
        {
            if (SetProperty(
                    ref _customStorageRoot,
                    value))
            {
                OnPropertyChanged(
                    nameof(HasCustomStorageRoot));

                OnPropertyChanged(
                    nameof(StorageLocationText));

                ResetStorageRootCommand
                    .NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasCustomStorageRoot =>
        !string.IsNullOrWhiteSpace(
            CustomStorageRoot);

    public string StorageLocationText =>
        HasCustomStorageRoot
            ? CustomStorageRoot!
            : "Default CopyGIF storage";

    public bool IsValid =>
        RecentLimit >=
            MinimumRecentLimit &&
        RecentLimit <=
            MaximumRecentLimit &&
        FavoriteLimit >=
            MinimumFavoriteLimit &&
        FavoriteLimit <=
            MaximumFavoriteLimit;

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

    private bool CanResetStorageRoot()
    {
        return
            !IsBusy &&
            HasCustomStorageRoot;
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
                "Loading library settings...");

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
                settings.Library);

            IsLoaded =
                true;

            OperationState =
                AsyncOperationState.Succeeded(
                    "Library settings loaded.");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Library settings load cancelled.");

            Message =
                UserMessage.Information(
                    "Loading library settings was cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to load library settings.");

            Message =
                UserMessage.Error(
                    "Unable to load library settings.",
                    "library_settings_load_failed");
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
                "Saving library settings...");

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
                    Library =
                        current.Library with
                        {
                            RecentLimit =
                                RecentLimit,

                            FavoriteLimit =
                                FavoriteLimit,

                            StoreFavoritesLocally =
                                StoreFavoritesLocally,

                            StoreRecentsLocally =
                                StoreRecentsLocally
                        }
                };

            SettingsSaveResult result =
                await _settingsCoordinator
                    .SaveAsync(
                        requested,
                        linkedCancellation.Token);

            ApplySettings(
                result.EffectiveSettings.Library);

            IsLoaded =
                true;

            if (result.Succeeded)
            {
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Library settings saved.");

                Message =
                    UserMessage.Success(
                        "Library settings saved.");

                return;
            }

            string message =
                string.IsNullOrWhiteSpace(
                    result.ErrorMessage)
                    ? "Unable to save library settings."
                    : result.ErrorMessage.Trim();

            OperationState =
                AsyncOperationState.Failed(
                    message);

            Message =
                UserMessage.Error(
                    message,
                    "library_settings_save_failed");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Library settings save cancelled.");

            Message =
                UserMessage.Information(
                    "Saving library settings was cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to save library settings.");

            Message =
                UserMessage.Error(
                    "Unable to save library settings.",
                    "library_settings_save_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task ChooseStorageRootAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        CancellationTokenSource operation =
            BeginOperation(
                "Choosing library location...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    operation.Token,
                    cancellationToken);

        try
        {
            SettingsSaveResult? result =
                await _settingsCoordinator
                    .ChooseLibraryStorageRootAsync(
                        linkedCancellation.Token);

            if (result is null)
            {
                OperationState =
                    AsyncOperationState.Cancelled(
                        "Library location unchanged.");

                Message =
                    null;

                return;
            }

            ApplySettings(
                result.EffectiveSettings.Library);

            IsLoaded =
                true;

            if (result.Succeeded)
            {
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Library location updated.");

                Message =
                    UserMessage.Success(
                        "Library location updated.");

                return;
            }

            string message =
                string.IsNullOrWhiteSpace(
                    result.ErrorMessage)
                    ? "Unable to change the library location."
                    : result.ErrorMessage.Trim();

            OperationState =
                AsyncOperationState.Failed(
                    message);

            Message =
                UserMessage.Error(
                    message,
                    "library_location_change_failed");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Library location change cancelled.");

            Message =
                UserMessage.Information(
                    "Changing the library location was cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to change the library location.");

            Message =
                UserMessage.Error(
                    "Unable to change the library location.",
                    "library_location_change_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task ResetStorageRootAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!HasCustomStorageRoot)
        {
            return;
        }

        CancellationTokenSource operation =
            BeginOperation(
                "Restoring default library location...");

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
                    Library =
                        current.Library with
                        {
                            CustomStorageRoot =
                                null
                        }
                };

            SettingsSaveResult result =
                await _settingsCoordinator
                    .SaveAsync(
                        requested,
                        linkedCancellation.Token);

            ApplySettings(
                result.EffectiveSettings.Library);

            IsLoaded =
                true;

            if (result.Succeeded)
            {
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Default library location restored.");

                Message =
                    UserMessage.Success(
                        "Default library location restored.");

                return;
            }

            string message =
                string.IsNullOrWhiteSpace(
                    result.ErrorMessage)
                    ? "Unable to restore the default library location."
                    : result.ErrorMessage.Trim();

            OperationState =
                AsyncOperationState.Failed(
                    message);

            Message =
                UserMessage.Error(
                    message,
                    "library_location_reset_failed");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Library location reset cancelled.");

            Message =
                UserMessage.Information(
                    "Restoring the default library location was cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to restore the default library location.");

            Message =
                UserMessage.Error(
                    "Unable to restore the default library location.",
                    "library_location_reset_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private void ApplySettings(
        LibrarySettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        RecentLimit =
            settings.RecentLimit;

        FavoriteLimit =
            settings.FavoriteLimit;

        StoreFavoritesLocally =
            settings.StoreFavoritesLocally;

        StoreRecentsLocally =
            settings.StoreRecentsLocally;

        CustomStorageRoot =
            settings.CustomStorageRoot;
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

        ChooseStorageRootCommand
            .NotifyCanExecuteChanged();

        ResetStorageRootCommand
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
