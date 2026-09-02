using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Application.Providers;

public sealed class ProviderCatalog :
    IProviderCatalog
{
    private readonly Dictionary<
        string,
        IGifProvider> _providersById;

    public ProviderCatalog(
        IEnumerable<IGifProvider> providers,
        IEnumerable<ProviderDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(
            providers);

        ArgumentNullException.ThrowIfNull(
            descriptors);

        IGifProvider[] providerArray =
            providers.ToArray();

        ProviderDescriptor[] descriptorArray =
            descriptors.ToArray();

        if (providerArray.Length == 0)
        {
            throw new InvalidOperationException(
                "No GIF providers are registered.");
        }

        if (descriptorArray.Length == 0)
        {
            throw new InvalidOperationException(
                "No GIF provider descriptors are registered.");
        }

        Dictionary<string, IGifProvider>
            providersById =
                CreateProviderDictionary(
                    providerArray);

        Dictionary<string, ProviderDescriptor>
            descriptorsById =
                CreateDescriptorDictionary(
                    descriptorArray);

        foreach (string providerId
                 in providersById.Keys)
        {
            if (!descriptorsById.ContainsKey(
                    providerId))
            {
                throw new InvalidOperationException(
                    $"GIF provider '{providerId}' does not have a descriptor.");
            }
        }

        foreach (string descriptorId
                 in descriptorsById.Keys)
        {
            if (!providersById.ContainsKey(
                    descriptorId))
            {
                throw new InvalidOperationException(
                    $"GIF provider descriptor '{descriptorId}' does not have an implementation.");
            }
        }

        _providersById =
            providersById;

        Providers =
            Array.AsReadOnly(
                descriptorArray);
    }

    public IReadOnlyList<ProviderDescriptor>
        Providers
    {
        get;
    }

    public IGifProvider GetRequiredProvider(
        string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            providerId);

        if (TryGetProvider(
                providerId,
                out IGifProvider? provider) &&
            provider is not null)
        {
            return provider;
        }

        throw new KeyNotFoundException(
            $"GIF provider '{providerId}' is not registered.");
    }

    public bool TryGetProvider(
        string providerId,
        out IGifProvider? provider)
    {
        if (string.IsNullOrWhiteSpace(
                providerId))
        {
            provider = null;

            return false;
        }

        return _providersById.TryGetValue(
            providerId.Trim(),
            out provider);
    }

    private static Dictionary<string, IGifProvider>
        CreateProviderDictionary(
            IEnumerable<IGifProvider> providers)
    {
        Dictionary<string, IGifProvider> result =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (IGifProvider provider
                 in providers)
        {
            ArgumentNullException.ThrowIfNull(
                provider);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                provider.Id);

            if (!result.TryAdd(
                    provider.Id,
                    provider))
            {
                throw new InvalidOperationException(
                    $"More than one GIF provider is registered with the ID '{provider.Id}'.");
            }
        }

        return result;
    }

    private static Dictionary<
        string,
        ProviderDescriptor>
        CreateDescriptorDictionary(
            IEnumerable<ProviderDescriptor>
                descriptors)
    {
        Dictionary<string, ProviderDescriptor>
            result =
                new(
                    StringComparer.OrdinalIgnoreCase);

        foreach (ProviderDescriptor descriptor
                 in descriptors)
        {
            ArgumentNullException.ThrowIfNull(
                descriptor);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                descriptor.Id);

            if (!result.TryAdd(
                    descriptor.Id,
                    descriptor))
            {
                throw new InvalidOperationException(
                    $"More than one GIF provider descriptor is registered with the ID '{descriptor.Id}'.");
            }
        }

        return result;
    }
}
