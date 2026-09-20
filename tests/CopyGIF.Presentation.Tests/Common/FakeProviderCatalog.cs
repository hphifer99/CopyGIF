using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Presentation.Tests.Common;

/// <summary>
/// A provider catalog that only describes providers. It is for tests of view models that read
/// provider settings (names, attribution, flags) and never call a provider.
/// </summary>
internal sealed class FakeProviderCatalog :
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
