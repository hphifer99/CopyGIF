using CopyGIF.Core.Contracts;

namespace CopyGIF.Application.Providers;

public interface IActiveGifProviderAccessor
{
    IGifProvider GetActiveProvider();
}