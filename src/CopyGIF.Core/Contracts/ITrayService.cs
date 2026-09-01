namespace CopyGIF.Core.Contracts;

public interface ITrayService :
    IAsyncDisposable
{
    event EventHandler? OpenRequested;

    event EventHandler? SettingsRequested;

    event EventHandler? ExitRequested;

    Task InitializeAsync(
        CancellationToken cancellationToken = default);

    Task ShowNotificationAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default);
}
