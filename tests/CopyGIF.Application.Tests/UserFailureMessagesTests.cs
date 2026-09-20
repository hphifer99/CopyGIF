using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using CopyGIF.Application;
using CopyGIF.Core.Models;

namespace CopyGIF.Application.Tests;

[TestClass]
public sealed class UserFailureMessagesTests
{
    private const string LeakyMessage = @"Could not access C:\Users\alice\AppData\Local\CopyGIF\settings.json (0x80070005) token=abc123";

    private static readonly object SinkGate = new();

    private static readonly Dictionary<string, string> ExpectedTexts = new(StringComparer.Ordinal)
    {
        ["AccessDenied"] = UserFailureMessages.AccessDenied,
        ["LocationMissing"] = UserFailureMessages.LocationMissing,
        ["FileProblem"] = UserFailureMessages.FileProblem,
        ["Network"] = UserFailureMessages.Network,
        ["KeyProtection"] = UserFailureMessages.KeyProtection,
        ["DataUnreadable"] = UserFailureMessages.DataUnreadable,
        ["Cancelled"] = UserFailureMessages.Cancelled,
        ["Generic"] = UserFailureMessages.Generic
    };

    [TestMethod]
    public void UserFacingException_IsShownAsWritten()
    {
        string text = UserFailureMessages.Describe(new UserFacingException("  KLIPY: The key was rejected.  "));

        Assert.AreEqual("KLIPY: The key was rejected.", text);
    }

    [TestMethod]
    public void UserFacingException_WithBlankMessage_UsesTheGenericText()
    {
        Assert.AreEqual(UserFailureMessages.Generic, UserFailureMessages.Describe(new UserFacingException("   ")));
    }

    [TestMethod]
    [DataRow(typeof(UnauthorizedAccessException), "AccessDenied")]
    [DataRow(typeof(FileNotFoundException), "LocationMissing")]
    [DataRow(typeof(DirectoryNotFoundException), "LocationMissing")]
    [DataRow(typeof(DriveNotFoundException), "LocationMissing")]
    [DataRow(typeof(PathTooLongException), "LocationMissing")]
    [DataRow(typeof(IOException), "FileProblem")]
    [DataRow(typeof(HttpRequestException), "Network")]
    [DataRow(typeof(TimeoutException), "Network")]
    [DataRow(typeof(CryptographicException), "KeyProtection")]
    [DataRow(typeof(InvalidDataException), "DataUnreadable")]
    [DataRow(typeof(JsonException), "DataUnreadable")]
    [DataRow(typeof(OperationCanceledException), "Cancelled")]
    [DataRow(typeof(InvalidOperationException), "Generic")]
    [DataRow(typeof(NullReferenceException), "Generic")]
    public void KnownExceptionTypes_MapToFixedText_AndNeverShowTheOriginalMessage(Type type, string expectedName)
    {
        Exception exception = (Exception)Activator.CreateInstance(type, LeakyMessage)!;

        string text = UserFailureMessages.Describe(exception);

        string expected = ExpectedTexts[expectedName];
        Assert.AreEqual(expected, text);
        Assert.IsFalse(text.Contains("alice", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("0x8007", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains("abc123", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AggregateException_DescribesTheFirstFailureAndSaysSomeChangesWereNotUndone()
    {
        AggregateException aggregate = new(
            "Apply failed and a previous API key could not be restored.",
            new IOException(LeakyMessage),
            new UnauthorizedAccessException(LeakyMessage));

        string text = UserFailureMessages.Describe(aggregate);

        Assert.AreEqual($"{UserFailureMessages.FileProblem} {UserFailureMessages.PartialRollback}", text);
        Assert.IsFalse(text.Contains("alice", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EmptyAggregateException_UsesTheGenericText()
    {
        Assert.AreEqual(UserFailureMessages.Generic, UserFailureMessages.Describe(new AggregateException()));
    }

    [TestMethod]
    public void Describe_WithStage_LogsTheTypeButNeverTheMessage()
    {
        List<string> lines = new();
        lock (SinkGate)
        {
            Action<string>? previous = RepairDiagnostics.Sink;
            RepairDiagnostics.Sink = line => { lock (lines) lines.Add(line); };
            try
            {
                UserFailureMessages.Describe("settings-apply-test", new IOException(LeakyMessage));
            }
            finally
            {
                RepairDiagnostics.Sink = previous;
            }
        }

        string[] mine;
        lock (lines) mine = lines.Where(l => l.Contains("settings-apply-test", StringComparison.Ordinal)).ToArray();
        Assert.HasCount(1, mine);
        StringAssert.Contains(mine[0], "exception=IOException");
        Assert.IsFalse(mine[0].Contains("alice", StringComparison.Ordinal));
        Assert.IsFalse(mine[0].Contains("abc123", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Describe_RejectsMissingArguments()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => UserFailureMessages.Describe(null!));
        Assert.ThrowsExactly<ArgumentException>(() => UserFailureMessages.Describe(" ", new IOException()));
    }
}
