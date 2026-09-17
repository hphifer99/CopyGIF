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
}
