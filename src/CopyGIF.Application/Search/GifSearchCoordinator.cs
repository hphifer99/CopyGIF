using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Search;

public sealed class GifSearchCoordinator :
    IGifSearchCoordinator
{
    private readonly IGifProvider _gifProvider;
    private readonly ISettingsStore _settingsStore;

    public GifSearchCoordinator(
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
    }

    public Task<GifSearchPage> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            query);

        return SearchCoreAsync(
            query,
            continuationToken: null,
            cancellationToken);
    }

    public Task<GifSearchPage> LoadMoreAsync(
        string query,
        string continuationToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            query);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            continuationToken);

        return SearchCoreAsync(
            query,
            continuationToken,
            cancellationToken);
    }

    private async Task<GifSearchPage>
        SearchCoreAsync(
            string query,
            string? continuationToken,
            CancellationToken cancellationToken)
    {
        AppSettings settings =
            await _settingsStore
                .LoadAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        int pageSize =
            Math.Clamp(
                settings.Search.ResultsPerSearch,
                1,
                50);

        GifSearchRequest request =
            new()
            {
                Query = query.Trim(),
                PageSize = pageSize,
                ContinuationToken =
                    continuationToken
            };

        return await _gifProvider
            .SearchAsync(
                request,
                cancellationToken)
            .ConfigureAwait(false);
    }
}