using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Search;

public sealed class SearchSuggestionCoordinator :
    ISearchSuggestionCoordinator,
    IDisposable
{
    private const int MaximumStoredQueryLength = 500;

    private readonly ISettingsStore _settingsStore;

    private readonly ISearchHistoryStore _historyStore;

    private readonly IClock _clock;

    private readonly SemaphoreSlim _historyGate =
        new(
            initialCount: 1,
            maxCount: 1);

    private bool _disposed;

    public SearchSuggestionCoordinator(
        ISettingsStore settingsStore,
        ISearchHistoryStore historyStore,
        IClock clock)
    {
        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        _historyStore =
            historyStore ??
            throw new ArgumentNullException(
                nameof(historyStore));

        _clock =
            clock ??
            throw new ArgumentNullException(
                nameof(clock));
    }

    public async Task<IReadOnlyList<string>>
        GetSuggestionsAsync(
            string input,
            int maximumResults = 8,
            CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        ArgumentNullException.ThrowIfNull(
            input);

        if (maximumResults < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumResults),
                maximumResults,
                "The maximum result count must be greater than zero.");
        }

        AppSettings settings =
            await LoadSettingsAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (!settings.Search.UseHistorySuggestions)
        {
            return [];
        }

        string normalizedInput =
            input.Trim();

        await _historyGate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            SearchHistorySnapshot snapshot =
                await _historyStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            return snapshot.Entries
                .Where(IsValidEntry)
                .Where(
                    entry =>
                        normalizedInput.Length == 0 ||
                        entry.Query.Contains(
                            normalizedInput,
                            StringComparison.OrdinalIgnoreCase))
                .GroupBy(
                    entry => entry.Query,
                    StringComparer.OrdinalIgnoreCase)
                .Select(
                    group =>
                        group
                            .OrderByDescending(
                                entry => entry.LastUsedAtUtc)
                            .ThenByDescending(
                                entry => entry.UseCount)
                            .First())
                .OrderByDescending(
                    entry =>
                        normalizedInput.Length > 0 &&
                        entry.Query.StartsWith(
                            normalizedInput,
                            StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(
                    entry => entry.LastUsedAtUtc)
                .ThenByDescending(
                    entry => entry.UseCount)
                .ThenBy(
                    entry => entry.Query,
                    StringComparer.OrdinalIgnoreCase)
                .Take(
                    maximumResults)
                .Select(
                    entry => entry.Query)
                .ToArray();
        }
        finally
        {
            _historyGate.Release();
        }
    }

    public async Task RecordSearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            query);

        string normalizedQuery =
            query.Trim();

        if (normalizedQuery.Length > MaximumStoredQueryLength)
        {
            throw new ArgumentException(
                $"The query cannot exceed {MaximumStoredQueryLength} characters.",
                nameof(query));
        }

        AppSettings settings =
            await LoadSettingsAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        if (!settings.Search.SaveSearchHistory)
        {
            return;
        }

        await _historyGate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            SearchHistorySnapshot snapshot =
                await _historyStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

            SearchHistoryEntry? existingEntry =
                snapshot.Entries
                    .Where(IsValidEntry)
                    .FirstOrDefault(
                        entry =>
                            string.Equals(
                                entry.Query,
                                normalizedQuery,
                                StringComparison.OrdinalIgnoreCase));

            int nextUseCount =
                existingEntry is null
                    ? 1
                    : existingEntry.UseCount == int.MaxValue
                        ? int.MaxValue
                        : existingEntry.UseCount + 1;

            SearchHistoryEntry updatedEntry =
                new()
                {
                    Query = normalizedQuery,
                    LastUsedAtUtc = _clock.UtcNow,
                    UseCount = nextUseCount
                };

            SearchHistoryEntry[] entries =
                snapshot.Entries
                    .Where(IsValidEntry)
                    .Where(
                        entry =>
                            !string.Equals(
                                entry.Query,
                                normalizedQuery,
                                StringComparison.OrdinalIgnoreCase))
                    .Prepend(
                        updatedEntry)
                    .GroupBy(
                        entry => entry.Query,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(
                        group =>
                            group
                                .OrderByDescending(
                                    entry => entry.LastUsedAtUtc)
                                .ThenByDescending(
                                    entry => entry.UseCount)
                                .First())
                    .OrderByDescending(
                        entry => entry.LastUsedAtUtc)
                    .ThenByDescending(
                        entry => entry.UseCount)
                    .ThenBy(
                        entry => entry.Query,
                        StringComparer.OrdinalIgnoreCase)
                    .Take(
                        settings.Search.SearchHistoryLimit)
                    .ToArray();

            await _historyStore
                .SaveAsync(
                    new SearchHistorySnapshot
                    {
                        Entries = entries
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _historyGate.Release();
        }
    }

    public async Task ClearHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        await _historyGate
            .WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await _historyStore
                .ClearAsync(
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _historyGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _historyGate.Dispose();
    }

    private async Task<AppSettings> LoadSettingsAsync(
        CancellationToken cancellationToken)
    {
        return AppSettingsNormalizer.Normalize(
            await _settingsStore
                .LoadAsync(
                    cancellationToken)
                .ConfigureAwait(false));
    }

    private static bool IsValidEntry(
        SearchHistoryEntry? entry)
    {
        return entry is not null &&
               !string.IsNullOrWhiteSpace(
                   entry.Query) &&
               entry.Query.Length <= MaximumStoredQueryLength &&
               entry.UseCount > 0;
    }
}
