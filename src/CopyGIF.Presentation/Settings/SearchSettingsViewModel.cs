using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Settings;
using CopyGIF.Core.Settings;
using CopyGIF.Presentation.Common;

namespace CopyGIF.Presentation.Settings;

public sealed class SearchSettingsViewModel :
    ObservableObject,
    IDisposable
{
    private readonly ISettingsCoordinator
        _settingsCoordinator;

    private CancellationTokenSource?
        _operationCancellation;

    private int _resultsPerSearch =
        24;

    private int _debounceMilliseconds =
        300;

    private bool _animatePreviews =
        true;

    private bool _autoLoadMoreResults;

    private bool _showTrendingWhenEmpty =
        true;

    private bool _saveSearchHistory =
        true;

    private bool _useHistorySuggestions =
        true;

    private int _searchHistoryLimit =
        50;

    private bool _isLoaded;

    private AsyncOperationState
        _operationState =
            AsyncOperationState.Idle;

    private UserMessage? _message;

    private bool _disposed;

    public SearchSettingsViewModel(
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

    public int MinimumResultsPerSearch
    { get; } =
        AppSettingsValidator
            .MinimumResultsPerSearch;

    public int MaximumResultsPerSearch
    { get; } =
        AppSettingsValidator
            .MaximumResultsPerSearch;

    public int MinimumDebounceMilliseconds
    { get; } =
        AppSettingsValidator
            .MinimumDebounceMilliseconds;

    public int MaximumDebounceMilliseconds
    { get; } =
        AppSettingsValidator
            .MaximumDebounceMilliseconds;

    public int MinimumSearchHistoryLimit
    { get; } =
        AppSettingsValidator
            .MinimumSearchHistoryLimit;

    public int MaximumSearchHistoryLimit
    { get; } =
        AppSettingsValidator
            .MaximumSearchHistoryLimit;

    public int ResultsPerSearch
    {
        get => _resultsPerSearch;

        set
        {
            if (SetProperty(
                    ref _resultsPerSearch,
                    value))
            {
                NotifyValidationChanged();
            }
        }
    }

    public int DebounceMilliseconds
    {
        get => _debounceMilliseconds;

        set
        {
            if (SetProperty(
                    ref _debounceMilliseconds,
                    value))
            {
                NotifyValidationChanged();
            }
        }
    }

    public bool AnimatePreviews
    {
        get => _animatePreviews;

        set =>
            SetProperty(
                ref _animatePreviews,
                value);
    }

    public bool AutoLoadMoreResults
    {
        get => _autoLoadMoreResults;

        set =>
            SetProperty(
                ref _autoLoadMoreResults,
                value);
    }

    public bool ShowTrendingWhenEmpty
    {
        get => _showTrendingWhenEmpty;

        set =>
            SetProperty(
                ref _showTrendingWhenEmpty,
                value);
    }

    public bool SaveSearchHistory
    {
        get => _saveSearchHistory;

        set =>
            SetProperty(
                ref _saveSearchHistory,
                value);
    }

    public bool UseHistorySuggestions
    {
        get => _useHistorySuggestions;

        set =>
            SetProperty(
                ref _useHistorySuggestions,
                value);
    }

    public int SearchHistoryLimit
    {
        get => _searchHistoryLimit;

        set
        {
            if (SetProperty(
                    ref _searchHistoryLimit,
                    value))
            {
                NotifyValidationChanged();
            }
        }
    }

    public bool IsValid =>
        ResultsPerSearch >=
            MinimumResultsPerSearch &&
        ResultsPerSearch <=
            MaximumResultsPerSearch &&
        DebounceMilliseconds >=
            MinimumDebounceMilliseconds &&
        DebounceMilliseconds <=
            MaximumDebounceMilliseconds &&
        SearchHistoryLimit >=
            MinimumSearchHistoryLimit &&
        SearchHistoryLimit <=
            MaximumSearchHistoryLimit;

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
                "Loading search settings...");

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
                settings.Search);

            IsLoaded =
                true;

            OperationState =
                AsyncOperationState.Succeeded(
                    "Search settings loaded.");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Search settings load cancelled.");

            Message =
                UserMessage.Information(
                    "Loading search settings was cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to load search settings.");

            Message =
                UserMessage.Error(
                    "Unable to load search settings.",
                    "search_settings_load_failed");
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
                "Saving search settings...");

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
                    Search =
                        current.Search with
                        {
                            ResultsPerSearch =
                                ResultsPerSearch,

                            DebounceMilliseconds =
                                DebounceMilliseconds,

                            AnimatePreviews =
                                AnimatePreviews,

                            AutoLoadMoreResults =
                                AutoLoadMoreResults,

                            ShowTrendingWhenEmpty =
                                ShowTrendingWhenEmpty,

                            SaveSearchHistory =
                                SaveSearchHistory,

                            UseHistorySuggestions =
                                UseHistorySuggestions,

                            SearchHistoryLimit =
                                SearchHistoryLimit
                        }
                };

            SettingsSaveResult result =
                await _settingsCoordinator
                    .SaveAsync(
                        requested,
                        linkedCancellation.Token);

            ApplySettings(
                result.EffectiveSettings.Search);

            IsLoaded =
                true;

            if (result.Succeeded)
            {
                OperationState =
                    AsyncOperationState.Succeeded(
                        "Search settings saved.");

                Message =
                    UserMessage.Success(
                        "Search settings saved.");

                return;
            }

            string message =
                string.IsNullOrWhiteSpace(
                    result.ErrorMessage)
                    ? "Unable to save search settings."
                    : result.ErrorMessage.Trim();

            OperationState =
                AsyncOperationState.Failed(
                    message);

            Message =
                UserMessage.Error(
                    message,
                    "search_settings_save_failed");
        }
        catch (OperationCanceledException)
            when (
                operation.IsCancellationRequested ||
                cancellationToken.IsCancellationRequested)
        {
            OperationState =
                AsyncOperationState.Cancelled(
                    "Search settings save cancelled.");

            Message =
                UserMessage.Information(
                    "Saving search settings was cancelled.");
        }
        catch (Exception)
        {
            OperationState =
                AsyncOperationState.Failed(
                    "Unable to save search settings.");

            Message =
                UserMessage.Error(
                    "Unable to save search settings.",
                    "search_settings_save_failed");
        }
        finally
        {
            EndOperation(
                operation);
        }
    }

    private void ApplySettings(
        SearchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        ResultsPerSearch =
            settings.ResultsPerSearch;

        DebounceMilliseconds =
            settings.DebounceMilliseconds;

        AnimatePreviews =
            settings.AnimatePreviews;

        AutoLoadMoreResults =
            settings.AutoLoadMoreResults;

        ShowTrendingWhenEmpty =
            settings.ShowTrendingWhenEmpty;

        SaveSearchHistory =
            settings.SaveSearchHistory;

        UseHistorySuggestions =
            settings.UseHistorySuggestions;

        SearchHistoryLimit =
            settings.SearchHistoryLimit;
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
