namespace CopyGIF.Core.Contracts;

public interface IStartupService
{
    Task<bool> IsEnabledAsync(
        CancellationToken cancellationToken = default);

    Task SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default);
}
