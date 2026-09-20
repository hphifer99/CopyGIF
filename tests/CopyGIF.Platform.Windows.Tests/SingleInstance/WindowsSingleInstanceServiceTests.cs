using System.IO.Pipes;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Platform.Windows.SingleInstance;

namespace CopyGIF.Platform.Windows.Tests.SingleInstance;

[TestClass]
public sealed class WindowsSingleInstanceServiceTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    [TestMethod]
    public async Task SecondInstance_IsRedirectedToThePrimary_WithItsArguments()
    {
        string instanceId = CreateInstanceId();
        await using WindowsSingleInstanceService primary = new(instanceId);
        await using WindowsSingleInstanceService second = new(instanceId);
        TaskCompletionSource<IReadOnlyList<string>> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.ActivationRequested += (_, args) => received.TrySetResult(args.Arguments);

        SingleInstanceResult first = await primary.InitializeAsync(Array.Empty<string>());
        SingleInstanceResult redirected = await second.InitializeAsync(new[] { "--open", "settings" });

        Assert.AreEqual(SingleInstanceStatus.PrimaryInstance, first.Status);
        Assert.AreEqual(SingleInstanceStatus.RedirectedToPrimary, redirected.Status);
        IReadOnlyList<string> arguments = await received.Task.WaitAsync(WaitLimit);
        CollectionAssert.AreEqual(new[] { "--open", "settings" }, arguments.ToArray());
    }

    // L-1: if the pipe is already owned, creating the listener fails. That used to be swallowed
    // and retried at once, which spins a CPU core. It must now wait between attempts.
    [TestMethod]
    public async Task BusyPipe_IsRetriedWithADelay_NotInASpin()
    {
        string instanceId = CreateInstanceId();
        using NamedPipeServerStream squatter = CreateSquatter(instanceId);
        await using WindowsSingleInstanceService service = new(
            instanceId, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(50));

        SingleInstanceResult result = await service.InitializeAsync(Array.Empty<string>());
        await Task.Delay(TimeSpan.FromMilliseconds(600));

        Assert.AreEqual(SingleInstanceStatus.PrimaryInstance, result.Status);
        // 600 ms at 50 ms per attempt is about 12 attempts. A spinning loop would make
        // thousands. The generous upper bound keeps the test stable on a slow machine.
        Assert.IsGreaterThanOrEqualTo(1, service.PipeCreationFailureCount);
        Assert.IsLessThanOrEqualTo(30, service.PipeCreationFailureCount);
    }

    [TestMethod]
    public async Task BusyPipe_BackoffGrowsUpToTheMaximum()
    {
        string instanceId = CreateInstanceId();
        using NamedPipeServerStream squatter = CreateSquatter(instanceId);
        // 100 ms, then 200 ms, then 200 ms. Over 1.5 seconds that is at most 9 attempts;
        // a fixed 100 ms delay would allow about 15.
        await using WindowsSingleInstanceService service = new(
            instanceId, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200));

        await service.InitializeAsync(Array.Empty<string>());
        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        Assert.IsLessThanOrEqualTo(11, service.PipeCreationFailureCount);
        Assert.IsGreaterThanOrEqualTo(3, service.PipeCreationFailureCount);
    }

    [TestMethod]
    public async Task BusyPipe_WhenItIsReleasedLater_TheListenerRecovers()
    {
        string instanceId = CreateInstanceId();
        NamedPipeServerStream squatter = CreateSquatter(instanceId);
        await using WindowsSingleInstanceService primary = new(
            instanceId, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(40));
        await using WindowsSingleInstanceService second = new(instanceId);
        TaskCompletionSource<IReadOnlyList<string>> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.ActivationRequested += (_, args) => received.TrySetResult(args.Arguments);

        await primary.InitializeAsync(Array.Empty<string>());
        await WaitUntilAsync(() => primary.PipeCreationFailureCount >= 1);
        squatter.Dispose();

        SingleInstanceResult redirected = await second.InitializeAsync(new[] { "--after-recovery" });

        Assert.AreEqual(SingleInstanceStatus.RedirectedToPrimary, redirected.Status);
        IReadOnlyList<string> arguments = await received.Task.WaitAsync(WaitLimit);
        CollectionAssert.AreEqual(new[] { "--after-recovery" }, arguments.ToArray());
    }

    [TestMethod]
    public async Task DisposeAsync_WhilePipeIsBusy_DoesNotWaitForTheRetryDelay()
    {
        string instanceId = CreateInstanceId();
        using NamedPipeServerStream squatter = CreateSquatter(instanceId);
        WindowsSingleInstanceService service = new(
            instanceId, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        await service.InitializeAsync(Array.Empty<string>());
        await WaitUntilAsync(() => service.PipeCreationFailureCount >= 1);

        Task dispose = service.DisposeAsync().AsTask();
        await dispose.WaitAsync(WaitLimit);

        Assert.IsTrue(dispose.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task InvalidRequest_DoesNotEndTheListener()
    {
        string instanceId = CreateInstanceId();
        await using WindowsSingleInstanceService primary = new(instanceId);
        await using WindowsSingleInstanceService second = new(instanceId);
        TaskCompletionSource<IReadOnlyList<string>> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.ActivationRequested += (_, args) => received.TrySetResult(args.Arguments);
        await primary.InitializeAsync(Array.Empty<string>());

        // A client that sends a length prefix that is not allowed, then leaves.
        await using (NamedPipeClientStream badClient = new(".", $"{instanceId}.Activation", PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await badClient.ConnectAsync(5000);
            await badClient.WriteAsync(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F });
            await badClient.FlushAsync();
        }

        SingleInstanceResult redirected = await second.InitializeAsync(new[] { "--still-listening" });

        Assert.AreEqual(SingleInstanceStatus.RedirectedToPrimary, redirected.Status);
        IReadOnlyList<string> arguments = await received.Task.WaitAsync(WaitLimit);
        CollectionAssert.AreEqual(new[] { "--still-listening" }, arguments.ToArray());
    }

    [TestMethod]
    public async Task FaultyActivationHandler_DoesNotStopOtherHandlersOrTheListener()
    {
        string instanceId = CreateInstanceId();
        await using WindowsSingleInstanceService primary = new(instanceId);
        TaskCompletionSource<IReadOnlyList<string>> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.ActivationRequested += (_, _) => throw new InvalidOperationException("faulty subscriber");
        primary.ActivationRequested += (_, args) => received.TrySetResult(args.Arguments);
        await primary.InitializeAsync(Array.Empty<string>());

        await using WindowsSingleInstanceService second = new(instanceId);
        SingleInstanceResult redirected = await second.InitializeAsync(new[] { "--x" });

        Assert.AreEqual(SingleInstanceStatus.RedirectedToPrimary, redirected.Status);
        IReadOnlyList<string> arguments = await received.Task.WaitAsync(WaitLimit);
        CollectionAssert.AreEqual(new[] { "--x" }, arguments.ToArray());
    }

    [TestMethod]
    public void Constructor_RejectsNonPositiveOrInvertedDelays()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new WindowsSingleInstanceService("id", TimeSpan.Zero, TimeSpan.FromSeconds(1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new WindowsSingleInstanceService("id", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
    }

    private static string CreateInstanceId() => $"CopyGIF.Test.{Guid.NewGuid():N}";

    // Owns the pipe name the service wants ("<id>.Activation"), allowing a single instance,
    // the same way a second logon session of the same user would.
    private static NamedPipeServerStream CreateSquatter(string instanceId) =>
        new($"{instanceId}.Activation", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow + WaitLimit;
        while (!condition())
        {
            Assert.IsLessThan(deadline, DateTime.UtcNow, "The condition was not met in time.");
            await Task.Delay(10);
        }
    }
}
