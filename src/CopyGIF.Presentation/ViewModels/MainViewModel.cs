using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CopyGIF.Application.Search;
using CopyGIF.Core.Models;

namespace CopyGIF.Presentation.ViewModels;

public sealed class MainViewModel :
    ObservableObject
{
    private readonly IGifSearchCoordinator
        _searchCoordinator;

    private CancellationTokenSource?
        _searchCancellation;

    private string _query =
        string.Empty;

    private bool _isSearching;

    private string _statusMessage =
        "Ready";

    private string? _continuationToken;

    public MainViewModel(
        IGifSearchCoordinator searchCoordinator)
    {
        _searchCoordinator =
            searchCoordinator ??
            throw new ArgumentNullException(
                nameof(searchCoordinator));

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

    public ObservableCollection<GifItem>
        Results
    { get; } =
            new();

    public IAsyncRelayCommand
        SearchCommand
    { get; }

    public IRelayCommand
        CancelSearchCommand
    { get; }

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
            !string.IsNullOrWhiteSpace(
                Query);
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
        if (string.IsNullOrWhiteSpace(
                Query))
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

        IsSearching =
            true;

        StatusMessage =
            "Searching...";

        Results.Clear();

        _continuationToken =
            null;

        OnPropertyChanged(
            nameof(HasMoreResults));

        try
        {
            GifSearchPage page =
                await _searchCoordinator
                    .SearchAsync(
                        searchQuery,
                        cancellation.Token);

            foreach (GifItem item
                     in page.Items)
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
            when (
                cancellation
                    .IsCancellationRequested)
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

    private static string
        GetProviderErrorMessage(
            GifProviderFailure failure)
    {
        return failure switch
        {
            GifProviderFailure.MissingCredential =>
                "A GIF provider API key is required.",

            GifProviderFailure.Unauthorized =>
                "The configured GIF provider rejected its API key.",

            GifProviderFailure.RateLimited =>
                "GIF searches are temporarily rate limited.",

            GifProviderFailure.Network =>
                "Unable to reach the GIF provider. Check your connection.",

            GifProviderFailure.ServiceUnavailable =>
                "The GIF provider is temporarily unavailable.",

            GifProviderFailure.InvalidResponse =>
                "The GIF provider returned an unexpected response.",

            _ =>
                "GIF search failed."
        };
    }
}