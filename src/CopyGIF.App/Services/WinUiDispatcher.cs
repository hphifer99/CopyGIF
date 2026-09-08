using Microsoft.UI.Dispatching;

namespace CopyGIF.App.Services;

public sealed class WinUiDispatcher
{
    private DispatcherQueue? _dispatcherQueue;

    public bool IsInitialized =>
        _dispatcherQueue is not null;

    public bool HasThreadAccess =>
        GetDispatcherQueue().HasThreadAccess;

    public void Initialize(
        DispatcherQueue dispatcherQueue)
    {
        ArgumentNullException.ThrowIfNull(
            dispatcherQueue);

        if (_dispatcherQueue is null)
        {
            _dispatcherQueue =
                dispatcherQueue;

            return;
        }

        if (!ReferenceEquals(
                _dispatcherQueue,
                dispatcherQueue))
        {
            throw new InvalidOperationException(
                "The UI dispatcher has already been initialized for another thread.");
        }
    }

    public bool TryEnqueue(
        Action action)
    {
        ArgumentNullException.ThrowIfNull(
            action);

        DispatcherQueue? dispatcherQueue =
            _dispatcherQueue;

        if (dispatcherQueue is null)
        {
            return false;
        }

        if (dispatcherQueue.HasThreadAccess)
        {
            action();

            return true;
        }

        return dispatcherQueue.TryEnqueue(
            () => action());
    }

    public Task InvokeAsync(
        Action action)
    {
        ArgumentNullException.ThrowIfNull(
            action);

        DispatcherQueue dispatcherQueue =
            GetDispatcherQueue();

        if (dispatcherQueue.HasThreadAccess)
        {
            action();

            return Task.CompletedTask;
        }

        TaskCompletionSource<object?> completion =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        bool wasQueued =
            dispatcherQueue.TryEnqueue(
                () =>
                {
                    try
                    {
                        action();

                        completion.SetResult(
                            null);
                    }
                    catch (Exception exception)
                    {
                        completion.SetException(
                            exception);
                    }
                });

        if (!wasQueued)
        {
            completion.SetException(
                new InvalidOperationException(
                    "The UI dispatcher is shutting down and rejected the operation."));
        }

        return completion.Task;
    }

    public Task InvokeAsync(
        Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(
            action);

        DispatcherQueue dispatcherQueue =
            GetDispatcherQueue();

        if (dispatcherQueue.HasThreadAccess)
        {
            return action();
        }

        TaskCompletionSource<object?> completion =
            new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);

        bool wasQueued =
            dispatcherQueue.TryEnqueue(
                async () =>
                {
                    try
                    {
                        await action()
                            .ConfigureAwait(true);

                        completion.SetResult(
                            null);
                    }
                    catch (Exception exception)
                    {
                        completion.SetException(
                            exception);
                    }
                });

        if (!wasQueued)
        {
            completion.SetException(
                new InvalidOperationException(
                    "The UI dispatcher is shutting down and rejected the operation."));
        }

        return completion.Task;
    }

    private DispatcherQueue GetDispatcherQueue()
    {
        return _dispatcherQueue ??
            throw new InvalidOperationException(
                "The UI dispatcher has not been initialized.");
    }
}
