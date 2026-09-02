using CopyGIF.Core.Contracts;

namespace CopyGIF.Platform.Windows.Tray;

public sealed class WindowsTrayService :
    ITrayService
{
    private const int MaximumTitleLength = 63;
    private const int MaximumMessageLength = 255;

    private readonly ITrayHost _host;
    private readonly SemaphoreSlim _initializationGate =
        new(1, 1);

    private bool _initialized;
    private int _disposeState;

    public WindowsTrayService()
        : this(
            new WindowsFormsTrayHost())
    {
    }

    internal WindowsTrayService(
        ITrayHost host)
    {
        _host =
            host ??
            throw new ArgumentNullException(
                nameof(host));

        _host.OpenRequested +=
            HandleOpenRequested;

        _host.SettingsRequested +=
            HandleSettingsRequested;

        _host.ExitRequested +=
            HandleExitRequested;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await _initializationGate.WaitAsync(
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            ThrowIfDisposed();

            if (_initialized)
            {
                return;
            }

            await _host.InitializeAsync(
                    cancellationToken)
                .ConfigureAwait(false);

            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public async Task ShowNotificationAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            title);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            message);

        await InitializeAsync(
                cancellationToken)
            .ConfigureAwait(false);

        await _host.ShowNotificationAsync(
                Truncate(
                    title.Trim(),
                    MaximumTitleLength),
                Truncate(
                    message.Trim(),
                    MaximumMessageLength),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _disposeState,
                1) != 0)
        {
            return;
        }

        _host.OpenRequested -=
            HandleOpenRequested;

        _host.SettingsRequested -=
            HandleSettingsRequested;

        _host.ExitRequested -=
            HandleExitRequested;

        await _initializationGate.WaitAsync()
            .ConfigureAwait(false);

        try
        {
            await _host.DisposeAsync()
                .ConfigureAwait(false);

            _initialized = false;
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

    internal static string Truncate(
        string value,
        int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(value);

        ArgumentOutOfRangeException
            .ThrowIfNegativeOrZero(
                maximumLength);

        if (value.Length <= maximumLength)
        {
            return value;
        }

        int length = maximumLength;

        if (char.IsHighSurrogate(
                value[length - 1]))
        {
            length--;
        }

        return value[..length];
    }

    private void HandleOpenRequested(
        object? sender,
        EventArgs eventArgs)
    {
        OpenRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void HandleSettingsRequested(
        object? sender,
        EventArgs eventArgs)
    {
        SettingsRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void HandleExitRequested(
        object? sender,
        EventArgs eventArgs)
    {
        ExitRequested?.Invoke(
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
