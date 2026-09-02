using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CopyGIF.Platform.Windows.Hotkeys;

internal sealed class WindowsHotkeyRegistrationHost :
    IHotkeyRegistrationHost
{
    private const int HotkeyMessage = 0x0312;
    private const uint NoRepeatModifier = 0x4000;
    private const int MaximumRegistrationId = 0xBFFF;

    private readonly TaskCompletionSource<HotkeyMessageWindow>
        _windowReady =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly Thread _messageThread;
    private int _currentRegistrationId;
    private int _nextRegistrationId;
    private int _disposeState;

    public WindowsHotkeyRegistrationHost()
    {
        _messageThread =
            new Thread(RunMessageLoop)
            {
                IsBackground = true,
                Name = "CopyGIF global hotkey"
            };

        _messageThread.SetApartmentState(
            ApartmentState.STA);

        _messageThread.Start();
    }

    public event EventHandler? Activated;

    public async Task<HotkeyNativeRegistrationResult>
        TryReplaceAsync(
            HotkeyGesture gesture,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        ThrowIfDisposed();

        cancellationToken.ThrowIfCancellationRequested();

        HotkeyMessageWindow window =
            await _windowReady.Task
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

        return await InvokeOnMessageThreadAsync(
                window,
                messageWindow =>
                    ReplaceRegistration(
                        messageWindow,
                        gesture))
            .ConfigureAwait(false);
    }

    public async Task UnregisterAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        HotkeyMessageWindow window =
            await _windowReady.Task
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

        await InvokeOnMessageThreadAsync(
                window,
                messageWindow =>
                {
                    UnregisterCurrent(
                        messageWindow);

                    return true;
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

        try
        {
            HotkeyMessageWindow window =
                await _windowReady.Task
                    .ConfigureAwait(false);

            await InvokeOnMessageThreadAsync(
                    window,
                    messageWindow =>
                    {
                        UnregisterCurrent(
                            messageWindow);

                        Application.ExitThread();
                        return true;
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
                    _messageThread.Join(
                        TimeSpan.FromSeconds(2)))
            .ConfigureAwait(false);

        Interlocked.Exchange(
            ref _disposeState,
            2);
    }

    private void RunMessageLoop()
    {
        try
        {
            using HotkeyMessageWindow window =
                new(HandleHotkeyMessage);

            _windowReady.TrySetResult(window);
            Application.Run();
        }
        catch (Exception exception)
        {
            _windowReady.TrySetException(exception);
        }
    }

    private HotkeyNativeRegistrationResult ReplaceRegistration(
        HotkeyMessageWindow window,
        HotkeyGesture gesture)
    {
        int candidateId =
            GetNextRegistrationId();

        bool candidateRegistered =
            RegisterHotKey(
                window.Handle,
                candidateId,
                checked((uint)gesture.Modifiers) |
                    NoRepeatModifier,
                gesture.VirtualKey);

        if (!candidateRegistered)
        {
            return new HotkeyNativeRegistrationResult(
                false,
                Marshal.GetLastWin32Error());
        }

        if (_currentRegistrationId != 0 &&
            !UnregisterHotKey(
                window.Handle,
                _currentRegistrationId))
        {
            int errorCode =
                Marshal.GetLastWin32Error();

            _ = UnregisterHotKey(
                window.Handle,
                candidateId);

            return new HotkeyNativeRegistrationResult(
                false,
                errorCode);
        }

        _currentRegistrationId = candidateId;

        return new HotkeyNativeRegistrationResult(
            true,
            0);
    }

    private void UnregisterCurrent(
        HotkeyMessageWindow window)
    {
        if (_currentRegistrationId == 0)
        {
            return;
        }

        if (!UnregisterHotKey(
                window.Handle,
                _currentRegistrationId))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows could not unregister the CopyGIF hotkey.");
        }

        _currentRegistrationId = 0;
    }

    private int GetNextRegistrationId()
    {
        if (_nextRegistrationId >=
            MaximumRegistrationId)
        {
            _nextRegistrationId = 0;
        }

        _nextRegistrationId++;
        return _nextRegistrationId;
    }

    private void RaiseActivated()
    {
        EventHandler? handlers = Activated;

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

    private void HandleHotkeyMessage(
        int registrationId)
    {
        if (registrationId ==
            _currentRegistrationId)
        {
            RaiseActivated();
        }
    }

    private static Task<TResult>
        InvokeOnMessageThreadAsync<TResult>(
            HotkeyMessageWindow window,
            Func<HotkeyMessageWindow, TResult> action)
    {
        TaskCompletionSource<TResult> completion =
            new(
                TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            _ = window.BeginInvoke(
                new Action(
                    () =>
                    {
                        try
                        {
                            completion.TrySetResult(
                                action(window));
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

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        nint windowHandle,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(
        nint windowHandle,
        int id);

    private sealed class HotkeyMessageWindow :
        Control
    {
        private readonly Action<int> _activated;

        public HotkeyMessageWindow(
            Action<int> activated)
        {
            _activated =
                activated ??
                throw new ArgumentNullException(
                    nameof(activated));

            _ = Handle;
        }

        protected override void SetVisibleCore(
            bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void WndProc(
            ref Message message)
        {
            if (message.Msg == HotkeyMessage &&
                message.WParam != nint.Zero)
            {
                _activated(
                    message.WParam.ToInt32());
            }

            base.WndProc(ref message);
        }
    }
}
