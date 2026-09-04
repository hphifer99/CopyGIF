namespace CopyGIF.Presentation.Common;

public enum AsyncOperationStatus
{
    Idle,
    Running,
    Succeeded,
    Cancelled,
    Failed
}

public sealed record AsyncOperationState
{
    public static AsyncOperationState Idle { get; } =
        new()
        {
            Status = AsyncOperationStatus.Idle
        };

    public AsyncOperationStatus Status { get; init; } =
        AsyncOperationStatus.Idle;

    public string Message { get; init; } =
        string.Empty;

    public bool IsBusy =>
        Status == AsyncOperationStatus.Running;

    public bool IsCompleted =>
        Status is
            AsyncOperationStatus.Succeeded or
            AsyncOperationStatus.Cancelled or
            AsyncOperationStatus.Failed;

    public bool IsSuccessful =>
        Status == AsyncOperationStatus.Succeeded;

    public bool IsCancelled =>
        Status == AsyncOperationStatus.Cancelled;

    public bool HasError =>
        Status == AsyncOperationStatus.Failed;

    public static AsyncOperationState Running(
        string? message = null)
    {
        return new AsyncOperationState
        {
            Status =
                AsyncOperationStatus.Running,

            Message =
                NormalizeMessage(
                    message)
        };
    }

    public static AsyncOperationState Succeeded(
        string? message = null)
    {
        return new AsyncOperationState
        {
            Status =
                AsyncOperationStatus.Succeeded,

            Message =
                NormalizeMessage(
                    message)
        };
    }

    public static AsyncOperationState Cancelled(
        string? message = null)
    {
        return new AsyncOperationState
        {
            Status =
                AsyncOperationStatus.Cancelled,

            Message =
                NormalizeMessage(
                    message)
        };
    }

    public static AsyncOperationState Failed(
        string? message = null)
    {
        return new AsyncOperationState
        {
            Status =
                AsyncOperationStatus.Failed,

            Message =
                NormalizeMessage(
                    message)
        };
    }

    private static string NormalizeMessage(
        string? message)
    {
        return string.IsNullOrWhiteSpace(
                message)
            ? string.Empty
            : message.Trim();
    }
}
