namespace CopyGIF.Platform.Windows.Tray;

internal interface ITrayHost :
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
