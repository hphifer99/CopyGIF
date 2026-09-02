using CopyGIF.Core.Contracts;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Providers;

public interface IActiveGifProviderAccessor
{
    IGifProvider GetActiveProvider(
        AppSettings settings);
}
