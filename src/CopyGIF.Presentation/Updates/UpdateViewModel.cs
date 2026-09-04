using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Updates;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;
using CoreInstallChannel =
    CopyGIF.Core.Models.InstallChannel;
using CoreInstallScope =
    CopyGIF.Core.Models.InstallScope;

namespace CopyGIF.Presentation.Updates;

public sealed class UpdateViewModel :
    ObservableObject,
    IDisposable
{
    private readonly IUpdateCoordinator
        _updateCoordinator;

    private CancellationTokenSource?
        _operationCancellation;

    private string _currentVersion =
        string.Empty;

    private UpdateCheckStatus?
        _checkStatus;

    private InstallationContext?
        _installation;

    private UpdateMode?
        _resolvedMode;

    private UpdateCandidate?
        _candidate;

    private DownloadedUpdatePackage?
        _preparedPackage;

    private DateTimeOffset?
        _lastCheckedAtUtc;

    private long _downloadedBytes;

    private long _totalBytes;

    private double _downloadPercentage;

    private AsyncOperationState
        _operationState =
            AsyncOperationState.Idle;

    private UserMessage? _message;

    private bool _disposed;

    public UpdateViewModel(
        IUpdateCoordinator updateCoordinator)
    {
        _updateCoordinator =
            updateCoordinator ??
            throw new ArgumentNullException(
                nameof(updateCoordinator));

        CheckCommand =
            new AsyncRelayCommand(
                CheckAsync,
                CanCheck);

        PrepareCommand =
            new AsyncRelayCommand(
                PrepareAsync,
                CanPrepare);

        InstallCommand =
            new AsyncRelayCommand(
                InstallAsync,
                CanInstall);

        RunAutomaticCommand =
            new AsyncRelayCommand(
                RunAutomaticAsync,
                CanCheck);

        CancelCommand =
            new RelayCommand(
                CancelOperation,
                CanCancel);
    }

    public IAsyncRelayCommand CheckCommand
    { get; }

    public IAsyncRelayCommand PrepareCommand
    { get; }

    public IAsyncRelayCommand InstallCommand
    { get; }

    public IAsyncRelayCommand RunAutomaticCommand
    { get; }

    public IRelayCommand CancelCommand
    { get; }

    public string CurrentVersion
    {
        get => _currentVersion;

        private set
        {
            if (SetProperty(
                    ref _currentVersion,
                    value))
            {
                OnPropertyChanged(
                    nameof(HasCurrentVersion));

                NotifyCommandStates();
            }
        }
    }

    public bool HasCurrentVersion =>
        !string.IsNullOrWhiteSpace(
            CurrentVersion);

    public UpdateCheckStatus? CheckStatus
    {
        get => _checkStatus;

        private set =>
            SetProperty(
                ref _checkStatus,
                value);
    }

    public InstallationContext? Installation
    {
        get => _installation;

        private set
        {
            if (SetProperty(
                    ref _installation,
                    value))
            {
                OnPropertyChanged(
                    nameof(InstallChannel));

                OnPropertyChanged(
                    nameof(InstallScope));

                OnPropertyChanged(
                    nameof(IsManagedByStore));

                NotifyCommandStates();
            }
        }
    }

    public CoreInstallChannel InstallChannel =>
        Installation?.Channel ??
        CoreInstallChannel.None;

    public CoreInstallScope InstallScope =>
        Installation?.Scope ??
        CoreInstallScope.None;

    public bool IsManagedByStore =>
        CheckStatus ==
            UpdateCheckStatus.ManagedByStore ||
        InstallChannel ==
            CoreInstallChannel.MicrosoftStore;

    public UpdateMode? ResolvedMode
    {
        get => _resolvedMode;

        private set =>
            SetProperty(
                ref _resolvedMode,
                value);
    }

    public UpdateCandidate? Candidate
    {
        get => _candidate;

        private set
        {
            if (SetProperty(
                    ref _candidate,
                    value))
            {
                OnPropertyChanged(
                    nameof(IsUpdateAvailable));

                OnPropertyChanged(
                    nameof(IsRequiredUpdate));

                OnPropertyChanged(
                    nameof(AvailableVersion));

                OnPropertyChanged(
                    nameof(ReleaseNotesUri));

                NotifyCommandStates();
            }
        }
    }

    public bool IsUpdateAvailable =>
        Candidate is not null;

    public bool IsRequiredUpdate =>
        Candidate?.IsRequired ??
        false;

    public string? AvailableVersion =>
        Candidate?.AvailableVersion;

    public Uri? ReleaseNotesUri =>
        Candidate?.ReleaseNotesUri;

    public DownloadedUpdatePackage?
        PreparedPackage
    {
        get => _preparedPackage;

        private set
        {
            if (SetProperty(
                    ref _preparedPackage,
                    value))
            {
                OnPropertyChanged(
                    nameof(IsPackageReady));

                NotifyCommandStates();
            }
        }
    }

    public bool IsPackageReady =>
        PreparedPackage is not null;

    public DateTimeOffset? LastCheckedAtUtc
    {
        get => _lastCheckedAtUtc;

        private set =>
            SetProperty(
                ref _lastCheckedAtUtc,
                value);
    }

    public long DownloadedBytes
    {
        get => _downloadedBytes;

        private set =>
            SetProperty(
                ref _downloadedBytes,
                value);
    }

    public long TotalBytes
    {
        get => _totalBytes;

        private set =>
            SetProperty(
                ref _totalBytes,
                value);
    }

    public double DownloadPercentage
    {
        get => _downloadPercentage;

        private set =>
            SetProperty(
                ref _downloadPercentage,
                value);
    }

    public bool HasDownloadProgress =>
        TotalBytes > 0;

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

    public void Initialize(
        string currentVersion)
    {
        ThrowIfDisposed();

        ArgumentException.ThrowIfNullOrWhiteSpace(
            currentVersion);

        if (IsBusy)
        {
            throw new InvalidOperationException(
                "The update view model cannot be reinitialized while an update operation is running.");
        }

        CurrentVersion =
            currentVersion.Trim();

        ResetUpdateState();

        OperationState =
            AsyncOperationState.Idle;

        Message =
            null;
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

    private bool CanCheck()
    {
        return
            !IsBusy &&
            HasCurrentVersion;
    }

    private bool CanPrepare()
    {
        return
            !IsBusy &&
            Candidate is not null &&
            PreparedPackage is null &&
            !IsManagedByStore;
    }

    private bool CanInstall()
    {
        return
            !IsBusy &&
            PreparedPackage is not null &&
            !IsManagedByStore;
    }

    private bool CanCancel()
    {
        return IsBusy;
    }

    private async Task CheckAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!HasCurrentVersion)
        {
            return;
        }

        CancellationTokenSource operation =
            BeginOperation(
                "Checking for updates...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    operation.Token,
                    cancellationToken);

        try
        {
            ResetCheckResult();

            UpdateCheckResult result =
                await _updateCoordinator
                    .CheckAsync(
                        CurrentVersion,
                        force: true,
                        linkedCancellation.Token);

            ApplyCheckResult(
                result);

            ApplyCheckPresentation(
                result);
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Update check cancelled.");

            Message =
                UserMessage.Information(
                    "Update check cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to check for updates.");

            Message =
                UserMessage.Error(
                    "Unable to check for updates.",
                    "update_check_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task PrepareAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        UpdateCandidate? candidate =
            Candidate;

        if (candidate is null ||
            IsManagedByStore)
        {
            return;
        }

        CancellationTokenSource operation =
            BeginOperation(
                "Downloading update...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    operation.Token,
                    cancellationToken);

        ResetDownloadProgress();

        IProgress<UpdateDownloadProgress> progress =
            new Progress<UpdateDownloadProgress>(
                ApplyDownloadProgress);

        try
        {
            UpdatePreparationResult result =
                await _updateCoordinator
                    .PrepareAsync(
                        candidate,
                        progress,
                        linkedCancellation.Token);

            if (!result.IsReady ||
                result.Package is null)
            {
                PreparedPackage =
                    null;

                ApplyVerificationFailure(
                    result.Verification);

                return;
            }

            PreparedPackage =
                result.Package;

            SetCompletedDownloadProgress(
                result.Package);

            OperationState =
                AsyncOperationState.Succeeded(
                    "Update downloaded and verified.");

            Message =
                UserMessage.Success(
                    $"CopyGIF {candidate.AvailableVersion} is downloaded and verified. It is ready to install.");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            PreparedPackage =
                null;

            OperationState =
                AsyncOperationState.Cancelled(
                    "Update download cancelled.");

            Message =
                UserMessage.Information(
                    "Update download cancelled.");
        }
        catch (Exception)
        {
            PreparedPackage =
                null;

            OperationState =
                AsyncOperationState.Failed(
                    "Unable to prepare update.");

            Message =
                UserMessage.Error(
                    "Unable to download and verify the update.",
                    "update_prepare_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task InstallAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        DownloadedUpdatePackage? package =
            PreparedPackage;

        if (package is null ||
            IsManagedByStore)
        {
            return;
        }

        CancellationTokenSource operation =
            BeginOperation(
                "Verifying update for installation...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    operation.Token,
                    cancellationToken);

        try
        {
            UpdateInstallationResult result =
                await _updateCoordinator
                    .InstallAsync(
                        package,
                        linkedCancellation.Token);

            switch (result.Status)
            {
                case UpdateInstallationStatus.Installed:
                    PreparedPackage =
                        null;

                    OperationState =
                        AsyncOperationState.Succeeded(
                            "Update installation started.");

                    Message =
                        UserMessage.Success(
                            "The verified CopyGIF update installer was started.");

                    break;

                case UpdateInstallationStatus.ManagedExternally:
                    PreparedPackage =
                        null;

                    OperationState =
                        AsyncOperationState.Succeeded(
                            "Updates are managed externally.");

                    Message =
                        UserMessage.Information(
                            "Updates for this installation are managed by Microsoft Store.");

                    break;

                case UpdateInstallationStatus.VerificationFailed:
                    PreparedPackage =
                        null;

                    ApplyVerificationFailure(
                        result.Verification);

                    break;

                default:
                    PreparedPackage =
                        null;

                    OperationState =
                        AsyncOperationState.Failed(
                            "Unable to install update.");

                    Message =
                        UserMessage.Error(
                            "Unable to install the update.",
                            "update_install_failed");

                    break;
            }
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Update installation cancelled.");

            Message =
                UserMessage.Information(
                    "Update installation cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to install update.");

            Message =
                UserMessage.Error(
                    "Unable to install the update.",
                    "update_install_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private async Task RunAutomaticAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!HasCurrentVersion)
        {
            return;
        }

        CancellationTokenSource operation =
            BeginOperation(
                "Checking for updates...");

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource
                .CreateLinkedTokenSource(
                    operation.Token,
                    cancellationToken);

        ResetCheckResult();

        IProgress<UpdateDownloadProgress> progress =
            new Progress<UpdateDownloadProgress>(
                ApplyDownloadProgress);

        try
        {
            AutomaticUpdateResult result =
                await _updateCoordinator
                    .RunAutomaticAsync(
                        CurrentVersion,
                        progress,
                        linkedCancellation.Token);

            ApplyCheckResult(
                result.Check);

            if (result.Preparation is
                {
                    IsReady: true,
                    Package: not null
                })
            {
                PreparedPackage =
                    result.Preparation.Package;

                SetCompletedDownloadProgress(
                    result.Preparation.Package);
            }

            ApplyAutomaticPresentation(
                result);
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Automatic update operation cancelled.");

            Message =
                UserMessage.Information(
                    "Automatic update operation cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Automatic update operation failed.");

            Message =
                UserMessage.Error(
                    "Unable to complete the automatic update check.",
                    "automatic_update_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private void ApplyCheckResult(
        UpdateCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        CheckStatus =
            result.Status;

        Installation =
            result.Installation;

        ResolvedMode =
            result.ResolvedMode;

        Candidate =
            result.Candidate;

        LastCheckedAtUtc =
            result.State.LastCheckedAtUtc;
    }

    private void ApplyCheckPresentation(
        UpdateCheckResult result)
    {
        switch (result.Status)
        {
            case UpdateCheckStatus.Disabled:
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Update checks are disabled.");

                Message =
                    UserMessage.Information(
                        "Automatic update checks are disabled in Settings.");

                break;

            case UpdateCheckStatus.ManagedByStore:
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Updates are managed by Microsoft Store.");

                Message =
                    UserMessage.Information(
                        "Microsoft Store manages updates for this installation.");

                break;

            case UpdateCheckStatus.NotDue:
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Update check is not due.");

                Message =
                    UserMessage.Information(
                        "The next scheduled update check is not due yet.");

                break;

            case UpdateCheckStatus.NoUpdateAvailable:
                OperationState =
                    AsyncOperationState.Succeeded(
                        "CopyGIF is up to date.");

                Message =
                    UserMessage.Success(
                        "You are using the latest available version of CopyGIF.");

                break;

            case UpdateCheckStatus.UpdateAvailable:
                if (result.Candidate is null)
                {
                    OperationState =
                        AsyncOperationState.Failed(
                            "Update information is incomplete.");

                    Message =
                        UserMessage.Error(
                            "The update service reported an update without release information.",
                            "update_candidate_missing");

                    return;
                }

                OperationState =
                    AsyncOperationState.Succeeded(
                        $"CopyGIF {result.Candidate.AvailableVersion} is available.");

                Message =
                    result.Candidate.IsRequired
                        ? UserMessage.Warning(
                            $"CopyGIF {result.Candidate.AvailableVersion} is required because this installed version is no longer supported.",
                            "update_required")
                        : UserMessage.Information(
                            $"CopyGIF {result.Candidate.AvailableVersion} is available.",
                            "update_available");

                break;

            default:
                OperationState =
                    AsyncOperationState.Failed(
                        "Unknown update status.");

                Message =
                    UserMessage.Error(
                        "The update service returned an unknown status.",
                        "update_status_unknown");

                break;
        }
    }

    private void ApplyAutomaticPresentation(
        AutomaticUpdateResult result)
    {
        switch (result.Action)
        {
            case AutomaticUpdateAction.None:
                ApplyCheckPresentation(
                    result.Check);

                break;

            case AutomaticUpdateAction.Notify:
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Update available.");

                Message =
                    CreateAvailableUpdateMessage(
                        result.Check.Candidate);

                break;

            case AutomaticUpdateAction.Prompt:
                if (PreparedPackage is null)
                {
                    OperationState =
                        AsyncOperationState.Failed(
                            "Prepared update is unavailable.");

                    Message =
                        UserMessage.Error(
                            "The update was prepared but no verified package is available.",
                            "update_package_missing");

                    return;
                }

                OperationState =
                    AsyncOperationState.Succeeded(
                        "Update ready to install.");

                Message =
                    UserMessage.Success(
                        $"CopyGIF {PreparedPackage.Manifest.Version} is downloaded, verified, and ready to install.");

                break;

            case AutomaticUpdateAction.Installed:
                PreparedPackage =
                    null;

                OperationState =
                    AsyncOperationState.Succeeded(
                        "Update installation started.");

                Message =
                    UserMessage.Success(
                        "The verified CopyGIF update installer was started.");

                break;

            case AutomaticUpdateAction.VerificationFailed:
                PreparedPackage =
                    null;

                UpdatePackageVerificationResult?
                    verification =
                        result.Preparation?.Verification ??
                        result.Installation?.Verification;

                if (verification is null)
                {
                    OperationState =
                        AsyncOperationState.Failed(
                            "Update verification failed.");

                    Message =
                        UserMessage.Error(
                            "The update package could not be verified.",
                            "update_verification_failed");

                    return;
                }

                ApplyVerificationFailure(
                    verification);

                break;

            default:
                PreparedPackage =
                    null;

                OperationState =
                    AsyncOperationState.Failed(
                        "Automatic update operation failed.");

                Message =
                    UserMessage.Error(
                        "The automatic update operation returned an unknown action.",
                        "automatic_update_unknown");

                break;
        }
    }

    private static UserMessage CreateAvailableUpdateMessage(
        UpdateCandidate? candidate)
    {
        if (candidate is null)
        {
            return UserMessage.Error(
                "Update release information is unavailable.",
                "update_candidate_missing");
        }

        return candidate.IsRequired
            ? UserMessage.Warning(
                $"CopyGIF {candidate.AvailableVersion} is required because this installed version is no longer supported.",
                "update_required")
            : UserMessage.Information(
                $"CopyGIF {candidate.AvailableVersion} is available.",
                "update_available");
    }

    private void ApplyVerificationFailure(
        UpdatePackageVerificationResult verification)
    {
        string message =
            GetVerificationFailureMessage(
                verification);

        OperationState =
            AsyncOperationState.Failed(
                message);

        Message =
            UserMessage.Error(
                message,
                GetVerificationFailureCode(
                    verification.Failure));
    }

    private static string GetVerificationFailureMessage(
        UpdatePackageVerificationResult verification)
    {
        return verification.Failure switch
        {
            UpdatePackageVerificationFailure.FileMissing =>
                "The downloaded update package could not be found.",

            UpdatePackageVerificationFailure.SizeMismatch =>
                "The downloaded update package has an unexpected size.",

            UpdatePackageVerificationFailure.HashMismatch =>
                "The downloaded update package failed its SHA-256 integrity check.",

            UpdatePackageVerificationFailure.InvalidSignature =>
                "The downloaded update package has an invalid Windows signature.",

            UpdatePackageVerificationFailure.UntrustedPublisher =>
                "The downloaded update package was not signed by the expected publisher.",

            UpdatePackageVerificationFailure.UnsupportedPackage =>
                "The downloaded update package is not a supported CopyGIF installer.",

            _ when !string.IsNullOrWhiteSpace(
                verification.Message) =>
                    verification.Message.Trim(),

            _ =>
                "The downloaded update package could not be verified."
        };
    }

    private static string GetVerificationFailureCode(
        UpdatePackageVerificationFailure failure)
    {
        return failure switch
        {
            UpdatePackageVerificationFailure.FileMissing =>
                "update_file_missing",

            UpdatePackageVerificationFailure.SizeMismatch =>
                "update_size_mismatch",

            UpdatePackageVerificationFailure.HashMismatch =>
                "update_hash_mismatch",

            UpdatePackageVerificationFailure.InvalidSignature =>
                "update_signature_invalid",

            UpdatePackageVerificationFailure.UntrustedPublisher =>
                "update_publisher_untrusted",

            UpdatePackageVerificationFailure.UnsupportedPackage =>
                "update_package_unsupported",

            _ =>
                "update_verification_failed"
        };
    }

    private void ApplyDownloadProgress(
        UpdateDownloadProgress progress)
    {
        DownloadedBytes =
            progress.BytesReceived;

        TotalBytes =
            progress.TotalBytes;

        DownloadPercentage =
            progress.Percentage;

        OnPropertyChanged(
            nameof(HasDownloadProgress));
    }

    private void SetCompletedDownloadProgress(
        DownloadedUpdatePackage package)
    {
        DownloadedBytes =
            package.SizeBytes;

        TotalBytes =
            package.SizeBytes;

        DownloadPercentage =
            100;

        OnPropertyChanged(
            nameof(HasDownloadProgress));
    }

    private void ResetDownloadProgress()
    {
        DownloadedBytes =
            0;

        TotalBytes =
            0;

        DownloadPercentage =
            0;

        OnPropertyChanged(
            nameof(HasDownloadProgress));
    }

    private void ResetCheckResult()
    {
        CheckStatus =
            null;

        Installation =
            null;

        ResolvedMode =
            null;

        Candidate =
            null;

        PreparedPackage =
            null;

        LastCheckedAtUtc =
            null;

        ResetDownloadProgress();
    }

    private void ResetUpdateState()
    {
        ResetCheckResult();
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
        CheckCommand
            .NotifyCanExecuteChanged();

        PrepareCommand
            .NotifyCanExecuteChanged();

        InstallCommand
            .NotifyCanExecuteChanged();

        RunAutomaticCommand
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
