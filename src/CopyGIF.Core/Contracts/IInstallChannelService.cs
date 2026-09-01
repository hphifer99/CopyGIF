using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IInstallChannelService
{
    Task<InstallationContext> GetCurrentAsync(
        CancellationToken cancellationToken = default);
}
