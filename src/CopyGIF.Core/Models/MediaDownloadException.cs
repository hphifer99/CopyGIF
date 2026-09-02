namespace CopyGIF.Core.Models;

public sealed class MediaDownloadException :
    Exception
{
    public MediaDownloadException(
        MediaDownloadFailure failure,
        string message,
        Exception? innerException = null,
        int? httpStatusCode = null)
        : base(
            message,
            innerException)
    {
        if (httpStatusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(
                nameof(httpStatusCode),
                httpStatusCode,
                "HTTP status codes must be between 100 and 599.");
        }

        Failure = failure;
        HttpStatusCode = httpStatusCode;
    }

    public MediaDownloadFailure Failure { get; }

    public int? HttpStatusCode { get; }
}
