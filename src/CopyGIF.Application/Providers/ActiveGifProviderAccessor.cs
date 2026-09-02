using CopyGIF.Core.Contracts;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Providers;

public sealed class ActiveGifProviderAccessor :
    IActiveGifProviderAccessor
{
    private readonly IProviderCatalog
        _providerCatalog;

    public ActiveGifProviderAccessor(
        IProviderCatalog providerCatalog)
    {
        _providerCatalog =
            providerCatalog ??
            throw new ArgumentNullException(
                nameof(providerCatalog));
    }

    public IGifProvider GetActiveProvider(
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        AppSettings normalized =
            AppSettingsNormalizer.Normalize(
                settings);

        if (_providerCatalog.TryGetProvider(
                normalized.Providers
                    .ActiveProviderId,
                out IGifProvider? provider) &&
            provider is not null)
        {
            return provider;
        }

        if (_providerCatalog.TryGetProvider(
                AppSettings.DefaultProviderId,
                out provider) &&
            provider is not null)
        {
            return provider;
        }

        throw new InvalidOperationException(
            $"The configured GIF provider " +
            $"'{normalized.Providers.ActiveProviderId}' " +
            "is unavailable and the default provider " +
            $"'{AppSettings.DefaultProviderId}' is not registered.");
    }
}
