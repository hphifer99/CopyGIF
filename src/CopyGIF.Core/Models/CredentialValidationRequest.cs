namespace CopyGIF.Core.Models;

public sealed record CredentialValidationResult
{
    public required bool IsValid { get; init; }

    public string? Message { get; init; }

    public static CredentialValidationResult Valid()
    {
        return new CredentialValidationResult
        {
            IsValid = true
        };
    }

    public static CredentialValidationResult Invalid(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new CredentialValidationResult
        {
            IsValid = false,
            Message = message
        };
    }
}