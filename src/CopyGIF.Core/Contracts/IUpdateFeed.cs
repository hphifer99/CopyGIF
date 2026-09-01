using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IUpdateFeed
{
    Task<UpdateManifest?> GetLatestAsync(
        string channel,
        CancellationToken cancellationToken = default);
}
