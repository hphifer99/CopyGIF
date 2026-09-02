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

    private readonly string _mutexName;
    private readonly string _pipeName;
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
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            instanceId);

        _mutexName =
            $"Local\\{instanceId}.Instance";

        _pipeName =
            $"{instanceId}.Activation";
    }

    public event EventHandler<ActivationRequestedEventArgs>?
        ActivationRequested;

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
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using NamedPipeServerStream server =
                    new(
                        _pipeName,
                        PipeDirection.InOut,
                        maxNumberOfServerInstances: 1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous |
                        PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(
                        cancellationToken)
                    .ConfigureAwait(false);

                IReadOnlyList<string> arguments =
                    await SingleInstanceProtocol
                        .ReadArgumentsAsync(
                            server,
                            cancellationToken)
                        .ConfigureAwait(false);

                await SingleInstanceProtocol
                    .WriteAcknowledgementAsync(
                        server,
                        cancellationToken)
                    .ConfigureAwait(false);

                RaiseActivationRequested(
                    arguments);
            }
            catch (OperationCanceledException)
                when (cancellationToken
                    .IsCancellationRequested)
            {
                break;
            }
            catch (IOException)
            {
            }
            catch (InvalidDataException)
            {
            }
        }
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

                await SingleInstanceProtocol
                    .WriteArgumentsAsync(
                        client,
                        arguments,
                        cancellationToken)
                    .ConfigureAwait(false);

                await SingleInstanceProtocol
                    .ReadAcknowledgementAsync(
                        client,
                        cancellationToken)
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
            catch (Exception)
            {
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
