using CopyGIF.Core.Contracts;

namespace CopyGIF.Application.Providers;

public sealed class ActiveGifProviderAccessor :
    IActiveGifProviderAccessor
{
    private readonly IReadOnlyList<IGifProvider>
        _providers;

    public ActiveGifProviderAccessor(
        IEnumerable<IGifProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(
            providers);

        IGifProvider[] providerArray =
            providers.ToArray();

        if (providerArray.Length == 0)
        {
            throw new InvalidOperationException(
                "No GIF providers are registered.");
        }

        string? duplicateProviderId =
            providerArray
                .GroupBy(
                    provider => provider.Id,
                    StringComparer.OrdinalIgnoreCase)
                .Where(
                    group => group.Count() > 1)
                .Select(
                    group => group.Key)
                .FirstOrDefault();

        if (duplicateProviderId is not null)
        {
            throw new InvalidOperationException(
                $"More than one GIF provider is registered with the ID '{duplicateProviderId}'.");
        }

        _providers =
            providerArray;
    }

    public IGifProvider GetActiveProvider()
    {
        //
        // V2 currently has one provider.
        //
        // Future provider selection will be implemented
        // here without changing SearchCoordinator,
        // SetupCoordinator, or ViewModels.
        //

        return _providers[0];
    }
}