namespace CopyGIF.Core.Models;

public enum HotkeyRegistrationFailure
{
    None,
    InvalidGesture,
    Conflict,
    SystemRejected
}

public sealed record HotkeyRegistrationResult
{
    public required bool Succeeded { get; init; }

    public HotkeyRegistrationFailure Failure { get; init; }

    public string? Message { get; init; }

    public static HotkeyRegistrationResult Success()
    {
        return new HotkeyRegistrationResult
        {
            Succeeded = true,
            Failure = HotkeyRegistrationFailure.None
        };
    }

    public static HotkeyRegistrationResult Failed(
        HotkeyRegistrationFailure failure,
        string message)
    {
        if (failure ==
            HotkeyRegistrationFailure.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failure),
                failure,
                "A failed registration requires a failure reason.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            message);

        return new HotkeyRegistrationResult
        {
            Succeeded = false,
            Failure = failure,
            Message = message
        };
    }
}
