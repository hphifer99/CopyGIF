namespace CopyGIF.Core.Contracts;

public interface IUriLauncherService
{
    Task<bool> TryLaunchAsync(
        Uri uri,
        CancellationToken cancellationToken = default);
}
