using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Testing;

/// <summary>
/// A provider catalog that only knows descriptors. It is for tests of code that reads provider
/// settings (names, attribution, flags) and never calls a provider.
/// </summary>
public sealed class FakeProviderCatalog :
    IProviderCatalog
{
    public FakeProviderCatalog(
        params ProviderDescriptor[] providers)
    {
        Providers =
            providers.Length == 0
                ? BuiltInProviders.All
                : Array.AsReadOnly(
                    providers);
    }

    public IReadOnlyList<ProviderDescriptor>
        Providers
    {
        get;
    }

    public IGifProvider GetRequiredProvider(
        string providerId)
    {
        throw new NotSupportedException(
            "This catalog only describes providers.");
    }

    public bool TryGetProvider(
        string providerId,
        out IGifProvider? provider)
    {
        provider = null;

        return false;
    }
}
