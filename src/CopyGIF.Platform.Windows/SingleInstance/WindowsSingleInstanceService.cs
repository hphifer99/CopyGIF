using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Platform.Windows.SingleInstance;

public sealed class WindowsSingleInstanceService :
    ISingleInstanceService
{
    private const int ConnectionAttempts = 20;
    private const int ConnectionTimeoutMilliseconds = 100;
    private static readonly TimeSpan PipeMessageTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultInitialRetryDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan DefaultMaximumRetryDelay = TimeSpan.FromSeconds(30);

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly TimeSpan _initialRetryDelay;
    private readonly TimeSpan _maximumRetryDelay;
    private int _pipeCreationFailures;
    private readonly SemaphoreSlim _initializationGate =
        new(1, 1);

    private Mutex? _instanceMarker;
    private CancellationTokenSource? _listenerCancellation;
    private Task? _listenerTask;
    private SingleInstanceStatus? _status;
    private int _disposeState;

    public WindowsSingleInstanceService()
        : this(CreateDefaultInstanceId())
    {
    }

    internal WindowsSingleInstanceService(
        string instanceId)
        : this(
            instanceId,
            DefaultInitialRetryDelay,
            DefaultMaximumRetryDelay)
    {
    }

    // The retry delays are injectable so tests can observe the back-off without waiting.
    internal WindowsSingleInstanceService(
        string instanceId,
        TimeSpan initialRetryDelay,
        TimeSpan maximumRetryDelay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            instanceId);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            initialRetryDelay,
            TimeSpan.Zero);

        ArgumentOutOfRangeException.ThrowIfLessThan(
            maximumRetryDelay,
            initialRetryDelay);

        _initialRetryDelay = initialRetryDelay;
        _maximumRetryDelay = maximumRetryDelay;

        _mutexName =
            $"Local\\{instanceId}.Instance";

        _pipeName =
            $"{instanceId}.Activation";
    }

    public event EventHandler<ActivationRequestedEventArgs>?
        ActivationRequested;

    // How many times creating the listening pipe has failed since the service started.
    // Only tests read this; it shows that a busy pipe is retried with a delay, not in a spin.
    internal int PipeCreationFailureCount =>
        Volatile.Read(
            ref _pipeCreationFailures);

    public async Task<SingleInstanceResult> InitializeAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ThrowIfDisposed();

        await _initializationGate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            if (_status is not null)
            {
                return CreateResult(
                    _status.Value);
            }

            Mutex marker =
                new(
                    initiallyOwned: false,
                    _mutexName,
                    out bool createdNew);

            if (createdNew)
            {
                _instanceMarker = marker;
                _listenerCancellation = new();
                _listenerTask =
                    ListenForActivationAsync(
                        _listenerCancellation.Token);
                _status =
                    SingleInstanceStatus.PrimaryInstance;

                return CreateResult(
                    _status.Value);
            }

            marker.Dispose();

            await RedirectToPrimaryAsync(
                    arguments,
                    cancellationToken)
                .ConfigureAwait(false);

            _status =
                SingleInstanceStatus.RedirectedToPrimary;

            return CreateResult(
                _status.Value);
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposeState,
                1) != 0)
        {
            return;
        }

        await _initializationGate.WaitAsync()
            .ConfigureAwait(false);

        try
        {
            if (_listenerCancellation is not null)
            {
                await _listenerCancellation
                    .CancelAsync()
                    .ConfigureAwait(false);
            }

            if (_listenerTask is not null)
            {
                try
                {
                    await _listenerTask
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    // The listener handles its own failures, so this is a last line of defence:
                    // shutting down must never throw because the listener ended badly.
                    RepairDiagnostics.RecordException(
                        "single-instance-dispose",
                        exception);
                }
            }

            _listenerCancellation?.Dispose();
            _instanceMarker?.Dispose();

            _listenerCancellation = null;
            _listenerTask = null;
            _instanceMarker = null;
            _status = null;
        }
        finally
        {
            _initializationGate.Release();
            _initializationGate.Dispose();

            Interlocked.Exchange(
                ref _disposeState,
                2);
        }
    }

    private async Task ListenForActivationAsync(
        CancellationToken cancellationToken)
    {
        TimeSpan retryDelay =
            _initialRetryDelay;

        int consecutiveFailures = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream server;

            try
            {
                server =
                    CreateServer();
            }
            catch (Exception exception)
            {
                // The pipe is busy (for example another session of the same user already owns
                // it) or could not be created. Wait, with a growing delay, and try again. The
                // listener must never spin, and it must never end without leaving a trace.
                Interlocked.Increment(
                    ref _pipeCreationFailures);

                consecutiveFailures++;

                if (IsPowerOfTwo(consecutiveFailures))
                {
                    RepairDiagnostics.RecordException(
                        "single-instance-pipe",
                        exception);
                }

                if (!await TryDelayAsync(
                            retryDelay,
                            cancellationToken)
                        .ConfigureAwait(false))
                {
                    break;
                }

                retryDelay =
                    NextRetryDelay(
                        retryDelay);

                continue;
            }

            consecutiveFailures = 0;
            retryDelay = _initialRetryDelay;

            await using (server.ConfigureAwait(false))
            {
                try
                {
                    await HandleConnectionAsync(
                            server,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                    when (cancellationToken
                        .IsCancellationRequested)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    RepairDiagnostics.Record(
                        "single-instance-pipe",
                        "local",
                        "read-timeout");
                }
                catch (Exception exception)
                {
                    // An IOException or InvalidDataException means the other process went away
                    // or sent something that is not a valid request. Anything else is
                    // unexpected. In every case keep listening: ending the listener silently
                    // would make every later launch fail to reach this instance.
                    RepairDiagnostics.RecordException(
                        "single-instance-pipe",
                        exception);
                }
            }
        }
    }

    private NamedPipeServerStream CreateServer()
    {
        return
            new(
                _pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous |
                PipeOptions.CurrentUserOnly);
    }

    private async Task HandleConnectionAsync(
        NamedPipeServerStream server,
        CancellationToken cancellationToken)
    {
        await server.WaitForConnectionAsync(
                cancellationToken)
            .ConfigureAwait(false);

        using CancellationTokenSource messageDeadline =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        messageDeadline.CancelAfter(PipeMessageTimeout);

        IReadOnlyList<string> arguments =
            await SingleInstanceProtocol
                .ReadArgumentsAsync(
                    server,
                    messageDeadline.Token)
                .ConfigureAwait(false);

        await SingleInstanceProtocol
                .WriteAcknowledgementAsync(
                    server,
                    messageDeadline.Token)
            .ConfigureAwait(false);

        RaiseActivationRequested(
            arguments);
    }

    // Returns false when cancellation ended the wait.
    private static async Task<bool> TryDelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                    delay,
                    cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            return false;
        }
    }

    private TimeSpan NextRetryDelay(
        TimeSpan current)
    {
        TimeSpan doubled =
            TimeSpan.FromTicks(
                Math.Min(
                    current.Ticks * 2,
                    _maximumRetryDelay.Ticks));

        return doubled;
    }

    // Diagnostics are written for the 1st, 2nd, 4th, 8th... failure in a row so a pipe
    // that stays busy for hours does not fill the log.
    private static bool IsPowerOfTwo(
        int value)
    {
        return
            value > 0 &&
            (value & (value - 1)) == 0;
    }

    private async Task RedirectToPrimaryAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (int attempt = 0;
             attempt < ConnectionAttempts;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using NamedPipeClientStream client =
                new(
                    ".",
                    _pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

            try
            {
                await client.ConnectAsync(
                        ConnectionTimeoutMilliseconds,
                        cancellationToken)
                    .ConfigureAwait(false);

                using CancellationTokenSource messageDeadline =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                messageDeadline.CancelAfter(PipeMessageTimeout);

                await SingleInstanceProtocol
                    .WriteArgumentsAsync(
                        client,
                        arguments,
                        messageDeadline.Token)
                    .ConfigureAwait(false);

                await SingleInstanceProtocol
                    .ReadAcknowledgementAsync(
                        client,
                        messageDeadline.Token)
                    .ConfigureAwait(false);

                return;
            }
            catch (Exception exception)
                when (exception is
                    TimeoutException or
                    IOException)
            {
                lastException = exception;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException("The existing CopyGIF instance did not acknowledge activation.",
                    new TimeoutException("The activation pipe timed out."));
            }

            if (attempt < ConnectionAttempts - 1)
            {
                await Task.Delay(
                        TimeSpan.FromMilliseconds(50),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            "The existing CopyGIF instance could not be activated.",
            lastException);
    }

    private void RaiseActivationRequested(
        IReadOnlyList<string> arguments)
    {
        EventHandler<ActivationRequestedEventArgs>? handlers =
            ActivationRequested;

        if (handlers is null)
        {
            return;
        }

        ActivationRequestedEventArgs eventArgs =
            new(arguments);

        foreach (EventHandler<ActivationRequestedEventArgs> handler
                 in handlers.GetInvocationList()
                     .Cast<EventHandler<ActivationRequestedEventArgs>>())
        {
            try
            {
                handler(this, eventArgs);
            }
            catch (Exception exception)
            {
                // A faulty subscriber must not stop the others or the listener.
                RepairDiagnostics.RecordException(
                    "single-instance-activation-handler",
                    exception);
            }
        }
    }

    private static SingleInstanceResult CreateResult(
        SingleInstanceStatus status)
    {
        return new SingleInstanceResult
        {
            Status = status
        };
    }

    private static string CreateDefaultInstanceId()
    {
        using WindowsIdentity identity =
            WindowsIdentity.GetCurrent();

        string userIdentity =
            identity.User?
                .Value ??
            Environment.UserName;

        byte[] identityHash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    userIdentity));

        return
            $"CopyGIF.{Convert.ToHexString(identityHash.AsSpan(0, 12))}";
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(
                ref _disposeState) != 0,
            this);
    }
}
