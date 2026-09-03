namespace CopyGIF.Application.Search;

public interface ISearchSuggestionCoordinator
{
    Task<IReadOnlyList<string>> GetSuggestionsAsync(
        string input,
        int maximumResults = 8,
        CancellationToken cancellationToken = default);

    Task RecordSearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task ClearHistoryAsync(
        CancellationToken cancellationToken = default);
}
