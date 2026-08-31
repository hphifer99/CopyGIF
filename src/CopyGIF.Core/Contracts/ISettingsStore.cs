using CopyGIF.Core.Settings;

namespace CopyGIF.Core.Contracts;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);
}