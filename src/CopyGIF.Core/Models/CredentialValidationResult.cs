namespace CopyGIF.Core.Models;

public enum CredentialValidationFailure
{
    None,
    MissingCredential,
    InvalidCredential,
    RateLimited,
    Network,
    Timeout,
    ServiceUnavailable,
    Unknown
}

public sealed record CredentialValidationResult
{
    public required bool IsValid { get; init; }

    public CredentialValidationFailure Failure { get; init; }

    public string? Message { get; init; }

    public static CredentialValidationResult Valid()
    {
        return new CredentialValidationResult
        {
            IsValid = true,
            Failure = CredentialValidationFailure.None
        };
    }

    public static CredentialValidationResult Invalid(
        string message,
        CredentialValidationFailure failure =
            CredentialValidationFailure.InvalidCredential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message);

        if (failure ==
            CredentialValidationFailure.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failure),
                failure,
                "An invalid credential result must include a failure.");
        }

        return new CredentialValidationResult
        {
            IsValid = false,
            Failure = failure,
            Message = message
        };
    }
}
