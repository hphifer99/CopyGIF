using CopyGIF.Application.Providers;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Search;

public sealed class GifSearchCoordinator :
    IGifSearchCoordinator
{
    private readonly IActiveGifProviderAccessor
        _providerAccessor;

    private readonly ISettingsStore
        _settingsStore;

    public GifSearchCoordinator(
        IActiveGifProviderAccessor providerAccessor,
        ISettingsStore settingsStore)
    {
        _providerAccessor =
            providerAccessor ??
            throw new ArgumentNullException(
                nameof(providerAccessor));

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
            GifSearchKind.Search,
            query,
            continuationToken: null,
            cancellationToken);
    }

    public Task<GifSearchPage> TrendingAsync(
        CancellationToken cancellationToken = default)
    {
        return SearchCoreAsync(
            GifSearchKind.Trending,
            query: string.Empty,
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
            GifSearchKind.Search,
            query,
            continuationToken,
            cancellationToken);
    }

    public Task<GifSearchPage>
        LoadMoreTrendingAsync(
            string continuationToken,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            continuationToken);

        return SearchCoreAsync(
            GifSearchKind.Trending,
            query: string.Empty,
            continuationToken,
            cancellationToken);
    }

    private async Task<GifSearchPage>
        SearchCoreAsync(
            GifSearchKind kind,
            string query,
            string? continuationToken,
            CancellationToken cancellationToken)
    {
        AppSettings settings =
            AppSettingsNormalizer.Normalize(
                await _settingsStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false));

        int pageSize =
            Math.Clamp(
                settings.Search.ResultsPerSearch,
                1,
                50);

        IGifProvider provider =
            _providerAccessor
                .GetActiveProvider(
                    settings);

        GifSearchRequest request =
            new()
            {
                Query =
                    kind == GifSearchKind.Search
                        ? query.Trim()
                        : string.Empty,

                Kind = kind,

                PageSize = pageSize,

                ContinuationToken =
                    continuationToken
            };

        return await provider
            .SearchAsync(
                request,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
