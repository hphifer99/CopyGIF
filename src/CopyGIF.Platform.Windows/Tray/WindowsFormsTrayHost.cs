using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace CopyGIF.Platform.Windows.Tray;

internal sealed class WindowsFormsTrayHost :
    ITrayHost
{
    private readonly SemaphoreSlim _initializationGate =
        new(1, 1);

    private readonly TaskCompletionSource<TrayThreadState>
        _trayReady =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

    private Thread? _trayThread;
    private bool _initialized;
    private int _disposeState;

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

            _trayThread =
                new Thread(RunTrayMessageLoop)
                {
                    IsBackground = true,
                    Name = "CopyGIF notification area"
                };

            _trayThread.SetApartmentState(
                ApartmentState.STA);

            cancellationToken.ThrowIfCancellationRequested();

            _trayThread.Start();

            _ = await _trayReady.Task
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
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        TrayThreadState state =
            await _trayReady.Task
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

        await InvokeOnTrayThreadAsync(
                state.Dispatcher,
                () =>
                {
                    state.NotifyIcon.ShowBalloonTip(
                        timeout: 5000,
                        title,
                        message,
                        ToolTipIcon.Info);
                })
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

        await _initializationGate.WaitAsync()
            .ConfigureAwait(false);

        try
        {
            if (_initialized &&
                _trayThread is not null)
            {
                try
                {
                    TrayThreadState state =
                        await _trayReady.Task
                            .ConfigureAwait(false);

                    await InvokeOnTrayThreadAsync(
                            state.Dispatcher,
                            () =>
                            {
                                state.NotifyIcon.Visible = false;
                                Application.ExitThread();
                            })
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                    when (exception is
                        ObjectDisposedException or
                        InvalidOperationException)
                {
                }

                await Task.Run(
                        () =>
                            _trayThread.Join(
                                TimeSpan.FromSeconds(2)))
                    .ConfigureAwait(false);
            }

            _initialized = false;
            _trayThread = null;
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

    private void RunTrayMessageLoop()
    {
        try
        {
            using Control dispatcher = new();
            _ = dispatcher.Handle;

            using Icon icon =
                CreateTrayIcon();

            using ContextMenuStrip menu =
                new();

            using ToolStripMenuItem openItem =
                new("Open");

            using ToolStripMenuItem settingsItem =
                new("Settings");

            using ToolStripMenuItem exitItem =
                new("Exit");

            openItem.Click +=
                (_, _) =>
                    RaiseEvent(OpenRequested);

            settingsItem.Click +=
                (_, _) =>
                    RaiseEvent(SettingsRequested);

            exitItem.Click +=
                (_, _) =>
                    RaiseEvent(ExitRequested);

            menu.Items.Add(openItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(
                new ToolStripSeparator());
            menu.Items.Add(exitItem);

            using NotifyIcon notifyIcon =
                new()
                {
                    ContextMenuStrip = menu,
                    Icon = icon,
                    Text = "CopyGIF",
                    Visible = true
                };

            notifyIcon.DoubleClick +=
                (_, _) =>
                    RaiseEvent(OpenRequested);

            _trayReady.TrySetResult(
                new TrayThreadState(
                    dispatcher,
                    notifyIcon));

            Application.Run();

            notifyIcon.Visible = false;
        }
        catch (Exception exception)
        {
            _trayReady.TrySetException(exception);
        }
    }

    private void RaiseEvent(
        EventHandler? handlers)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler handler in
                 handlers.GetInvocationList()
                     .Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception)
            {
            }
        }
    }

    private static Icon CreateTrayIcon()
    {
        string? processPath =
            Environment.ProcessPath;

        if (!string.IsNullOrWhiteSpace(processPath))
        {
            try
            {
                Icon? extracted =
                    Icon.ExtractAssociatedIcon(
                        processPath);

                if (extracted is not null)
                {
                    return extracted;
                }
            }
            catch (Exception exception)
                when (exception is
                    ArgumentException or
                    FileNotFoundException or
                    Win32Exception)
            {
            }
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private static Task InvokeOnTrayThreadAsync(
        Control dispatcher,
        Action action)
    {
        TaskCompletionSource completion =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            _ = dispatcher.BeginInvoke(
                new Action(
                    () =>
                    {
                        try
                        {
                            action();
                            completion.TrySetResult();
                        }
                        catch (Exception exception)
                        {
                            completion.TrySetException(
                                exception);
                        }
                    }));
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }

        return completion.Task;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(
                ref _disposeState) != 0,
            this);
    }

    private sealed record TrayThreadState(
        Control Dispatcher,
        NotifyIcon NotifyIcon);
}
