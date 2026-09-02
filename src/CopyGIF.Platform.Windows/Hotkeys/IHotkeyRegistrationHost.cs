namespace CopyGIF.Platform.Windows.Hotkeys;

internal readonly record struct HotkeyNativeRegistrationResult(
    bool Succeeded,
    int ErrorCode);

internal interface IHotkeyRegistrationHost :
    IAsyncDisposable
{
    event EventHandler? Activated;

    Task<HotkeyNativeRegistrationResult> TryReplaceAsync(
        HotkeyGesture gesture,
        CancellationToken cancellationToken = default);

    Task UnregisterAsync(
        CancellationToken cancellationToken = default);
}
