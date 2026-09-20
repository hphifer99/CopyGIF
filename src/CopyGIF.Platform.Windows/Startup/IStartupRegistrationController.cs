namespace CopyGIF.Platform.Windows.Startup;

internal interface IStartupRegistrationController
{
    Task<bool> IsEnabledAsync(
        CancellationToken cancellationToken = default);

    Task SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default);
}
