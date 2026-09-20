using CopyGIF.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Core.Tests.Models;

[TestClass]
public sealed class RepairDiagnosticsTests
{
    private static readonly object SinkGate = new();

    [TestMethod]
    public void RecordException_WritesTypeAndOriginButNeverTheMessage()
    {
        string line = CaptureLine(
            () =>
            {
                try
                {
                    ThrowWithSensitiveMessage();
                }
                catch (InvalidOperationException exception)
                {
                    RepairDiagnostics.RecordException(
                        "unhandled-test",
                        exception);
                }
            });

        StringAssert.Contains(line, "stage=unhandled-test");
        StringAssert.Contains(line, "exception=InvalidOperationException");
        StringAssert.Contains(line, "origin=RepairDiagnosticsTests.ThrowWithSensitiveMessage");
        Assert.IsFalse(
            line.Contains("secret-token", StringComparison.Ordinal),
            "The exception message must not reach the repair log.");
        Assert.IsFalse(
            line.Contains("C:\\Users", StringComparison.Ordinal),
            "File paths from exception messages must not reach the repair log.");
    }

    [TestMethod]
    public void RecordException_UnwrapsAggregateExceptions()
    {
        AggregateException aggregate = new(
            new TimeoutException("query=cats"));

        string line = CaptureLine(
            () => RepairDiagnostics.RecordException(
                "unobserved-test",
                aggregate));

        StringAssert.Contains(line, "exception=TimeoutException");
        Assert.IsFalse(
            line.Contains("query=cats", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RecordException_ThrowingSinkNeverEscapes()
    {
        lock (SinkGate)
        {
            Action<string>? previous = RepairDiagnostics.Sink;

            try
            {
                RepairDiagnostics.Sink = _ => throw new IOException("disk full");

                RepairDiagnostics.RecordException(
                    "sink-failure-test",
                    new InvalidOperationException("boom"));
            }
            finally
            {
                RepairDiagnostics.Sink = previous;
            }
        }
    }

    [TestMethod]
    public void RecordException_RejectsBlankStageAndNullException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => RepairDiagnostics.RecordException(
                " ",
                new InvalidOperationException()));

        Assert.ThrowsExactly<ArgumentNullException>(
            () => RepairDiagnostics.RecordException(
                "stage",
                null!));
    }

    private static string CaptureLine(Action action)
    {
        lock (SinkGate)
        {
            Action<string>? previous = RepairDiagnostics.Sink;
            string? captured = null;

            try
            {
                RepairDiagnostics.Sink = message => captured = message;
                action();
            }
            finally
            {
                RepairDiagnostics.Sink = previous;
            }

            Assert.IsNotNull(captured);

            return captured;
        }
    }

    private static void ThrowWithSensitiveMessage() =>
        throw new InvalidOperationException(
            "secret-token at C:\\Users\\someone\\file.txt");
}
