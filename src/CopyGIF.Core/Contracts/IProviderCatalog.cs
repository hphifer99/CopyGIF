using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IProviderCatalog
{
    IReadOnlyList<ProviderDescriptor> Providers { get; }

    IGifProvider GetRequiredProvider(
        string providerId);

    bool TryGetProvider(
        string providerId,
        out IGifProvider? provider);
}
