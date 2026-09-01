using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface ISingleInstanceService :
    IAsyncDisposable
{
    event EventHandler<ActivationRequestedEventArgs>?
        ActivationRequested;

    Task<SingleInstanceResult> InitializeAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
