using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IHotkeyService
{
    event EventHandler? Activated;

    string? RegisteredGesture { get; }

    Task<HotkeyRegistrationResult> TryRegisterAsync(
        string gesture,
        CancellationToken cancellationToken = default);

    Task UnregisterAsync(
        CancellationToken cancellationToken = default);
}
