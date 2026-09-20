using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Platform.Windows.Hotkeys;

public sealed class WindowsHotkeyService :
    IHotkeyService,
    IAsyncDisposable
{
    private const int HotkeyAlreadyRegisteredError = 1409;

    private readonly IHotkeyRegistrationHost _host;
    private readonly SemaphoreSlim _gate =
        new(1, 1);
    private readonly object _stateLock = new();

    private string? _registeredGesture;
    private int _disposeState;

    public WindowsHotkeyService()
        : this(
            new WindowsHotkeyRegistrationHost())
    {
    }

    internal WindowsHotkeyService(
        IHotkeyRegistrationHost host)
    {
        _host =
            host ??
            throw new ArgumentNullException(
                nameof(host));

        _host.Activated +=
            HandleHostActivated;
    }

    public event EventHandler? Activated;

    public string? RegisteredGesture
    {
        get
        {
            lock (_stateLock)
            {
                return _registeredGesture;
            }
        }
    }

    public async Task<HotkeyRegistrationResult>
        TryRegisterAsync(
            string gesture,
            CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!HotkeyGestureParser.TryParse(
                gesture,
                out HotkeyGesture? parsedGesture,
                out string errorMessage))
        {
            return HotkeyRegistrationResult.Failed(
                HotkeyRegistrationFailure.InvalidGesture,
                errorMessage);
        }

        await _gate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            lock (_stateLock)
            {
                if (string.Equals(
                        _registeredGesture,
                        parsedGesture.CanonicalText,
                        StringComparison.Ordinal))
                {
                    return HotkeyRegistrationResult
                        .Success();
                }
            }

            HotkeyNativeRegistrationResult nativeResult =
                await _host.TryReplaceAsync(
                        parsedGesture,
                        cancellationToken)
                    .ConfigureAwait(false);

            if (!nativeResult.Succeeded)
            {
                return nativeResult.ErrorCode ==
                    HotkeyAlreadyRegisteredError
                        ? HotkeyRegistrationResult.Failed(
                            HotkeyRegistrationFailure.Conflict,
                            "That hotkey is already being used by Windows or another application.")
                        : HotkeyRegistrationResult.Failed(
                            HotkeyRegistrationFailure.SystemRejected,
                            "Windows rejected that global hotkey. The previous hotkey is still active.");
            }

            lock (_stateLock)
            {
                _registeredGesture =
                    parsedGesture.CanonicalText;
            }

            return HotkeyRegistrationResult.Success();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UnregisterAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            await _host.UnregisterAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            lock (_stateLock)
            {
                _registeredGesture = null;
            }
        }
        finally
        {
            _gate.Release();
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

        _host.Activated -=
            HandleHostActivated;

        await _gate.WaitAsync()
            .ConfigureAwait(false);

        try
        {
            await _host.DisposeAsync()
                .ConfigureAwait(false);

            lock (_stateLock)
            {
                _registeredGesture = null;
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();

            Interlocked.Exchange(
                ref _disposeState,
                2);
        }
    }

    private void HandleHostActivated(
        object? sender,
        EventArgs eventArgs)
    {
        Activated?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(
                ref _disposeState) != 0,
            this);
    }
}
