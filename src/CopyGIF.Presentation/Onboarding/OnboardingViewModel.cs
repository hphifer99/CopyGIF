using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Onboarding;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Common;

namespace CopyGIF.Presentation.Onboarding;

public sealed class OnboardingViewModel :
    ObservableObject,
    IDisposable
{
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

    private Uri? _credentialHelpUri;

    private bool _isRequired;

    private bool _isLoaded;

    private AsyncOperationState
        _operationState =
            AsyncOperationState.Idle;

    private UserMessage? _message;

    private bool _disposed;

    public OnboardingViewModel(
        IOnboardingCoordinator onboardingCoordinator)
    {
        _onboardingCoordinator =
            onboardingCoordinator ??
            throw new ArgumentNullException(
                nameof(onboardingCoordinator));

        _credentialHelpUri =
            onboardingCoordinator.CredentialHelpUri;

        LoadCommand =
            new AsyncRelayCommand(
                LoadAsync,
                CanStartOperation);

        CompleteCommand =
            new AsyncRelayCommand(
                CompleteAsync,
                CanComplete);

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

    public IAsyncRelayCommand CompleteCommand
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

                CompleteCommand
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

    public Uri? CredentialHelpUri
    {
        get => _credentialHelpUri;

        private set =>
            SetProperty(
                ref _credentialHelpUri,
                value);
    }

    public bool IsRequired
    {
        get => _isRequired;

        private set
        {
            if (SetProperty(
                    ref _isRequired,
                    value))
            {
                OnPropertyChanged(
                    nameof(IsCompleted));

                CompleteCommand
                    .NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsCompleted =>
        IsLoaded &&
        !IsRequired;

    public bool IsLoaded
    {
        get => _isLoaded;

        private set
        {
            if (SetProperty(
                    ref _isLoaded,
                    value))
            {
                OnPropertyChanged(
                    nameof(IsCompleted));

                CompleteCommand
                    .NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasCredentialInput =>
        !string.IsNullOrWhiteSpace(
            Credential);

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

    private bool CanComplete()
    {
        return
            !IsBusy &&
            IsLoaded &&
            IsRequired &&
            HasCredentialInput;
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
                "Checking setup...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                operation.Token,
                cancellationToken);

        try
        {
            OnboardingState state =
                await _onboardingCoordinator
                    .GetStateAsync(
                        linkedCancellation.Token);

            ApplyState(
                state);

            IsLoaded =
                true;

            OperationState =
                AsyncOperationState.Succeeded(
                    IsRequired
                        ? "Setup required."
                        : "Setup complete.");

            Message =
                IsRequired
                    ? UserMessage.Information(
                        $"Enter your {ProviderDisplayName} API key to finish setup.")
                    : null;
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Setup check cancelled.");

            Message =
                UserMessage.Information(
                    "Setup check cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to check setup.");

            Message =
                UserMessage.Error(
                    "Unable to determine the current setup state.",
                    "onboarding_load_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task CompleteAsync(
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
                await _onboardingCoordinator
                    .CompleteAsync(
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

            IsRequired =
                false;

            IsLoaded =
                true;

            OperationState =
                AsyncOperationState.Succeeded(
                    "Setup complete.");

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
                    "Unable to validate API key.");

            Message =
                UserMessage.Error(
                    "Unable to validate the API key.",
                    "onboarding_complete_failed");
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

                Message =
                    null;

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

    private void ApplyState(
        OnboardingState state)
    {
        ArgumentNullException.ThrowIfNull(
            state);

        ProviderId =
            state.ProviderId;

        ProviderDisplayName =
            state.ProviderDisplayName;

        CredentialHelpUri =
            state.CredentialHelpUri;

        IsRequired =
            state.IsRequired;
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

        CompleteCommand
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
