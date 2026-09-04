using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Credentials;
using CopyGIF.Application.Onboarding;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Common;

namespace CopyGIF.Presentation.Settings;

public sealed class ApiSettingsViewModel :
    ObservableObject,
    IDisposable
{
    private readonly IApiCredentialCoordinator
        _credentialCoordinator;

    private readonly IOnboardingCoordinator
        _onboardingCoordinator;

    private CancellationTokenSource?
        _operationCancellation;

    private string _credential =
        string.Empty;

    private string _providerId =
        string.Empty;

    private string _providerDisplayName =
        string.Empty;

    private bool _hasCredential;

    private bool _isLoaded;

    private AsyncOperationState
        _operationState =
            AsyncOperationState.Idle;

    private UserMessage? _message;

    private bool _disposed;

    public ApiSettingsViewModel(
        IApiCredentialCoordinator credentialCoordinator,
        IOnboardingCoordinator onboardingCoordinator)
    {
        _credentialCoordinator =
            credentialCoordinator ??
            throw new ArgumentNullException(
                nameof(credentialCoordinator));

        _onboardingCoordinator =
            onboardingCoordinator ??
            throw new ArgumentNullException(
                nameof(onboardingCoordinator));

        LoadCommand =
            new AsyncRelayCommand(
                LoadAsync,
                CanStartOperation);

        SaveCommand =
            new AsyncRelayCommand(
                SaveAsync,
                CanSave);

        DeleteCommand =
            new AsyncRelayCommand(
                DeleteAsync,
                CanDelete);

        OpenCredentialHelpCommand =
            new AsyncRelayCommand(
                OpenCredentialHelpAsync,
                CanStartOperation);

        CancelCommand =
            new RelayCommand(
                CancelOperation,
                CanCancel);
    }

    public IAsyncRelayCommand LoadCommand
    { get; }

    public IAsyncRelayCommand SaveCommand
    { get; }

    public IAsyncRelayCommand DeleteCommand
    { get; }

    public IAsyncRelayCommand OpenCredentialHelpCommand
    { get; }

    public IRelayCommand CancelCommand
    { get; }

    public string Credential
    {
        get => _credential;

        set
        {
            string normalized =
                value ??
                string.Empty;

            if (SetProperty(
                    ref _credential,
                    normalized))
            {
                OnPropertyChanged(
                    nameof(HasCredentialInput));

                SaveCommand
                    .NotifyCanExecuteChanged();
            }
        }
    }

    public string ProviderId
    {
        get => _providerId;

        private set =>
            SetProperty(
                ref _providerId,
                value);
    }

    public string ProviderDisplayName
    {
        get => _providerDisplayName;

        private set =>
            SetProperty(
                ref _providerDisplayName,
                value);
    }

    public Uri CredentialHelpUri =>
        _onboardingCoordinator
            .CredentialHelpUri;

    public bool HasCredential
    {
        get => _hasCredential;

        private set
        {
            if (SetProperty(
                    ref _hasCredential,
                    value))
            {
                OnPropertyChanged(
                    nameof(CredentialStatusText));

                DeleteCommand
                    .NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasCredentialInput =>
        !string.IsNullOrWhiteSpace(
            Credential);

    public string CredentialStatusText =>
        HasCredential
            ? "API key configured"
            : "API key not configured";

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

                DeleteCommand
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
            HasCredentialInput;
    }

    private bool CanDelete()
    {
        return
            !IsBusy &&
            IsLoaded &&
            HasCredential;
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
                "Loading API settings...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                operation.Token,
                cancellationToken);

        try
        {
            ApiCredentialState state =
                await _credentialCoordinator
                    .GetStateAsync(
                        linkedCancellation.Token);

            ProviderId =
                state.ProviderId;

            ProviderDisplayName =
                state.ProviderDisplayName;

            HasCredential =
                state.HasCredential;

            Credential =
                string.Empty;

            IsLoaded =
                true;

            OperationState =
                AsyncOperationState.Succeeded(
                    "API settings loaded.");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "API settings load cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to load API settings.");

            Message =
                UserMessage.Error(
                    "Unable to load API key status.",
                    "api_settings_load_failed");
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

        string credential =
            Credential.Trim();

        if (string.IsNullOrWhiteSpace(
                credential))
        {
            return;
        }

        CancellationTokenSource operation =
            BeginOperation(
                "Validating API key...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                operation.Token,
                cancellationToken);

        try
        {
            CredentialValidationResult result =
                await _credentialCoordinator
                    .ValidateAndSaveAsync(
                        credential,
                        linkedCancellation.Token);

            if (!result.IsValid)
            {
                ApplyValidationFailure(
                    result);

                return;
            }

            Credential =
                string.Empty;

            HasCredential =
                true;

            OperationState =
                AsyncOperationState.Succeeded(
                    "API key saved.");

            Message =
                UserMessage.Success(
                    $"{ProviderDisplayName} API key validated and saved.");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "API key validation cancelled.");

            Message =
                UserMessage.Information(
                    "API key validation cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to save API key.");

            Message =
                UserMessage.Error(
                    "Unable to validate and save the API key.",
                    "api_settings_save_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task DeleteAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!HasCredential)
        {
            return;
        }

        CancellationTokenSource operation =
            BeginOperation(
                "Removing API key...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                operation.Token,
                cancellationToken);

        try
        {
            await _credentialCoordinator
                .DeleteAsync(
                    linkedCancellation.Token);

            Credential =
                string.Empty;

            HasCredential =
                false;

            OperationState =
                AsyncOperationState.Succeeded(
                    "API key removed.");

            Message =
                UserMessage.Success(
                    $"{ProviderDisplayName} API key removed.");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "API key removal cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to remove API key.");

            Message =
                UserMessage.Error(
                    "Unable to remove the API key.",
                    "api_settings_delete_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task OpenCredentialHelpAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        CancellationTokenSource operation =
            BeginOperation(
                "Opening developer page...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                operation.Token,
                cancellationToken);

        try
        {
            bool opened =
                await _onboardingCoordinator
                    .OpenCredentialHelpAsync(
                        linkedCancellation.Token);

            if (opened)
            {
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Developer page opened.");

                return;
            }

            OperationState =
                AsyncOperationState.Failed(
                    "Unable to open developer page.");

            Message =
                UserMessage.Warning(
                    "Unable to open the API key page.",
                    "credential_help_failed");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Open page cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to open developer page.");

            Message =
                UserMessage.Warning(
                    "Unable to open the API key page.",
                    "credential_help_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private void ApplyValidationFailure(
        CredentialValidationResult result)
    {
        string message =
            GetValidationFailureMessage(
                result);

        OperationState =
            AsyncOperationState.Failed(
                message);

        Message =
            result.Failure switch
            {
                CredentialValidationFailure.RateLimited =>
                    UserMessage.Warning(
                        message,
                        "credential_rate_limited"),

                CredentialValidationFailure.Network =>
                    UserMessage.Warning(
                        message,
                        "credential_network"),

                CredentialValidationFailure.Timeout =>
                    UserMessage.Warning(
                        message,
                        "credential_timeout"),

                CredentialValidationFailure.ServiceUnavailable =>
                    UserMessage.Warning(
                        message,
                        "credential_service_unavailable"),

                _ =>
                    UserMessage.Error(
                        message,
                        "credential_invalid")
            };
    }

    private static string GetValidationFailureMessage(
        CredentialValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(
                result.Message))
        {
            return result.Message.Trim();
        }

        return result.Failure switch
        {
            CredentialValidationFailure.MissingCredential =>
                "An API key is required.",

            CredentialValidationFailure.InvalidCredential =>
                "The API key was rejected.",

            CredentialValidationFailure.RateLimited =>
                "API key validation is temporarily rate limited.",

            CredentialValidationFailure.Network =>
                "Unable to reach the GIF provider. Check your connection.",

            CredentialValidationFailure.Timeout =>
                "The GIF provider took too long to respond.",

            CredentialValidationFailure.ServiceUnavailable =>
                "The GIF provider is temporarily unavailable.",

            _ =>
                "Unable to validate the API key."
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

        DeleteCommand
            .NotifyCanExecuteChanged();

        OpenCredentialHelpCommand
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
