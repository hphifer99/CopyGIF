namespace CopyGIF.Core.Models;

public sealed class GifProviderException :
    Exception
{
    public GifProviderException(
        string providerId,
        GifProviderFailure failure,
        string message,
        Exception? innerException = null,
        TimeSpan? retryAfter = null)
        : base(
            message,
            innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            providerId);

        if (retryAfter < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryAfter),
                retryAfter,
                "RetryAfter cannot be negative.");
        }

        ProviderId = providerId;
        Failure = failure;
        RetryAfter = retryAfter;
    }

    public string ProviderId { get; }

    public GifProviderFailure Failure { get; }

    public TimeSpan? RetryAfter { get; }
}
