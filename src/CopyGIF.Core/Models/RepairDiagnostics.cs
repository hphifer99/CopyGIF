using System.Diagnostics;

namespace CopyGIF.Core.Models;

public static class RepairDiagnostics
{
    public static Action<string>? Sink { get; set; }
    public static void Record(string stage, string providerId, string result, long bytes = 0)
    {
        // Callers pass fixed stage/result names, never URLs, search queries or credential values.
        string message = $"{DateTimeOffset.UtcNow:O} CopyGIF stage={stage} provider={providerId} result={result} bytes={bytes}";
        Trace.WriteLine(message);
        try { Sink?.Invoke(message); } catch { /* Diagnostics must never fail the user action. */ }
    }

    /// <summary>
    /// Records that an exception escaped to a global handler or a window-level failure path.
    /// Only the exception type and the name of the method that threw are written. The message
    /// is deliberately left out because messages can contain file paths, URLs or query text.
    /// </summary>
    public static void RecordException(string stage, Exception exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stage);
        ArgumentNullException.ThrowIfNull(exception);

        Exception root = exception is AggregateException aggregate && aggregate.InnerExceptions.Count > 0
            ? aggregate.InnerExceptions[0]
            : exception;

        string origin = "unknown";

        try
        {
            System.Reflection.MethodBase? method = root.TargetSite;

            if (method is not null)
            {
                origin = $"{method.DeclaringType?.Name ?? "?"}.{method.Name}";
            }
        }
        catch
        {
            // Reading the throwing method is best effort and must never fail the caller.
        }

        RecordContext(
            stage,
            $"exception={root.GetType().Name} origin={origin}");
    }

    public static void RecordContext(string stage, string detail)
    {
        // Callers pass fixed stage names and local folder paths, never credential values.
        string message = $"{DateTimeOffset.UtcNow:O} CopyGIF stage={stage} {detail}";
        Trace.WriteLine(message);
        try { Sink?.Invoke(message); } catch { /* Diagnostics must never fail the user action. */ }
    }
}
