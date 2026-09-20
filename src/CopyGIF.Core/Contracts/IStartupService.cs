namespace CopyGIF.Core.Contracts;

public interface IStartupService
{
    Task<bool> IsEnabledAsync(
        CancellationToken cancellationToken = default);

    Task SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the real Windows registration state, or null when this run has no
    /// registration target (for example an unpackaged development run), so callers
    /// must keep the saved preference instead of trusting a meaningless "off".
    /// </summary>
    async Task<bool?> GetRegistrationStateAsync(
        CancellationToken cancellationToken = default) =>
        await IsEnabledAsync(
                cancellationToken)
            .ConfigureAwait(false);
}
