using System.Security.Cryptography;
using System.Text.Json;
using CopyGIF.Core.Models;

namespace CopyGIF.Application;

/// <summary>
/// Turns an exception that reached a window into text that is safe and useful to show.
/// Only <see cref="UserFacingException"/> messages are shown as written. Everything else is
/// mapped by exception type to a fixed sentence, because framework messages can include local
/// paths, HRESULT text or other detail that is unlocalized and confusing. The exception type
/// and the throwing method (never the message) are written to the repair log.
/// </summary>
public static class UserFailureMessages
{
    public const string Generic =
        "Something went wrong and nothing was changed. Please try again. If it keeps happening, restart CopyGIF.";

    public const string Cancelled =
        "The operation was cancelled. Nothing was changed.";

    public const string AccessDenied =
        "CopyGIF was not allowed to write to its settings or library folder. Check the folder permissions, then try again.";

    public const string LocationMissing =
        "A folder or file that CopyGIF needs could not be found. Check that the storage location is available, then try again.";

    public const string FileProblem =
        "CopyGIF could not read or save a file. Check that the drive is available and has free space, then try again.";

    public const string Network =
        "CopyGIF could not reach the network. Check your internet connection, then try again.";

    public const string KeyProtection =
        "Windows could not protect or read a stored API key. Enter the key again.";

    public const string DataUnreadable =
        "CopyGIF found a data file that it could not read. Restart CopyGIF and try again.";

    public const string PartialRollback =
        "Some changes could not be undone, so please check your settings before trying again.";

    /// <summary>Logs the exception under <paramref name="stage"/> and returns the text to show.</summary>
    public static string Describe(string stage, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentNullException.ThrowIfNull(exception);

        RepairDiagnostics.RecordException(stage, exception);
        return Describe(exception);
    }

    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        switch (exception)
        {
            case UserFacingException userFacing:
                return string.IsNullOrWhiteSpace(userFacing.Message)
                    ? Generic
                    : userFacing.Message.Trim();

            case OperationCanceledException:
                return Cancelled;

            case AggregateException aggregate when aggregate.InnerExceptions.Count > 0:
                // An aggregate is used when a change failed and the undo failed as well.
                return $"{Describe(aggregate.InnerExceptions[0])} {PartialRollback}";

            case UnauthorizedAccessException:
                return AccessDenied;

            // These are all IOException subtypes, so they must come before the IOException case.
            case FileNotFoundException:
            case DirectoryNotFoundException:
            case DriveNotFoundException:
            case PathTooLongException:
                return LocationMissing;

            case InvalidDataException:
            case JsonException:
                return DataUnreadable;

            case IOException:
                return FileProblem;

            case HttpRequestException:
            case TimeoutException:
            case System.Net.Sockets.SocketException:
                return Network;

            case CryptographicException:
                return KeyProtection;

            default:
                return Generic;
        }
    }
}
