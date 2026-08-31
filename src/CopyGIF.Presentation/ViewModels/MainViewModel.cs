using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Presentation.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IGifProvider _gifProvider;
    private readonly ISettingsStore _settingsStore;

    private CancellationTokenSource? _searchCancellation;

    private string _query = string.Empty;
    private bool _isSearching;
    private string _statusMessage = "Ready";
    private string? _continuationToken;

    public MainViewModel(
        IGifProvider gifProvider,
        ISettingsStore settingsStore)
    {
        _gifProvider =
            gifProvider ??
            throw new ArgumentNullException(
                nameof(gifProvider));

        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        Results.CollectionChanged +=
            (_, _) =>
            {
                OnPropertyChanged(
                    nameof(ResultCount));

                OnPropertyChanged(
                    nameof(HasResults));
            };

        SearchCommand =
            new AsyncRelayCommand(
                SearchAsync,
                CanSearch);

        CancelSearchCommand =
            new RelayCommand(
                CancelSearch,
                CanCancelSearch);
    }

    public ObservableCollection<GifItem> Results { get; } =
        new();

    public IAsyncRelayCommand SearchCommand { get; }

    public IRelayCommand CancelSearchCommand { get; }

    public string Query
    {
        get => _query;

        set
        {
            if (SetProperty(
                    ref _query,
                    value))
            {
                SearchCommand
                    .NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsSearching
    {
        get => _isSearching;

        private set
        {
            if (SetProperty(
                    ref _isSearching,
                    value))
            {
                SearchCommand
                    .NotifyCanExecuteChanged();

                CancelSearchCommand
                    .NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;

        private set =>
            SetProperty(
                ref _statusMessage,
                value);
    }

    public int ResultCount =>
        Results.Count;

    public bool HasResults =>
        Results.Count > 0;

    public bool HasMoreResults =>
        !string.IsNullOrWhiteSpace(
            _continuationToken);

    private bool CanSearch()
    {
        return
            !IsSearching &&
            !string.IsNullOrWhiteSpace(Query);
    }

    private bool CanCancelSearch()
    {
        return IsSearching;
    }

    private void CancelSearch()
    {
        _searchCancellation?.Cancel();
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            return;
        }

        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();

        CancellationTokenSource cancellation =
            new();

        _searchCancellation =
            cancellation;

        string searchQuery =
            Query.Trim();

        IsSearching = true;

        StatusMessage =
            "Searching...";

        Results.Clear();

        _continuationToken = null;

        OnPropertyChanged(
            nameof(HasMoreResults));

        try
        {
            AppSettings settings =
                await _settingsStore.LoadAsync(
                    cancellation.Token);

            int pageSize =
                Math.Clamp(
                    settings.Search.ResultsPerSearch,
                    1,
                    50);

            GifSearchPage page =
                await _gifProvider.SearchAsync(
                    new GifSearchRequest
                    {
                        Query = searchQuery,
                        PageSize = pageSize
                    },
                    cancellation.Token);

            foreach (GifItem item in page.Items)
            {
                Results.Add(item);
            }

            _continuationToken =
                page.ContinuationToken;

            OnPropertyChanged(
                nameof(HasMoreResults));

            StatusMessage =
                Results.Count switch
                {
                    0 =>
                        "No results found.",

                    1 =>
                        "1 result",

                    _ =>
                        $"{Results.Count} results"
                };
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            StatusMessage =
                "Search cancelled.";
        }
        catch (GifProviderException exception)
        {
            StatusMessage =
                GetProviderErrorMessage(
                    exception.Failure);
        }
        catch (Exception)
        {
            StatusMessage =
                "GIF search failed.";
        }
        finally
        {
            if (ReferenceEquals(
                    _searchCancellation,
                    cancellation))
            {
                _searchCancellation =
                    null;

                IsSearching =
                    false;
            }

            cancellation.Dispose();
        }
    }

    private static string GetProviderErrorMessage(
        GifProviderFailure failure)
    {
        return failure switch
        {
            GifProviderFailure.MissingCredential =>
                "KLIPY API key required.",

            GifProviderFailure.Unauthorized =>
                "KLIPY rejected the configured API key.",

            GifProviderFailure.RateLimited =>
                "KLIPY is temporarily rate limiting searches.",

            GifProviderFailure.Network =>
                "Unable to reach KLIPY. Check your connection.",

            GifProviderFailure.ServiceUnavailable =>
                "KLIPY is temporarily unavailable.",

            GifProviderFailure.InvalidResponse =>
                "KLIPY returned an unexpected response.",

            _ =>
                "GIF search failed."
        };
    }
}