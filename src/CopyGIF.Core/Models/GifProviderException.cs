namespace CopyGIF.Core.Models;

public sealed class GifProviderException : Exception
{
    public GifProviderException(
        string providerId,
        GifProviderFailure failure,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        ProviderId = providerId;
        Failure = failure;
    }

    public string ProviderId { get; }

    public GifProviderFailure Failure { get; }
}