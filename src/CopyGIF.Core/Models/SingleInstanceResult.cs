namespace CopyGIF.Core.Models;

public enum SingleInstanceStatus
{
    PrimaryInstance,
    RedirectedToPrimary
}

public sealed record SingleInstanceResult
{
    public required SingleInstanceStatus Status { get; init; }

    public bool IsPrimaryInstance =>
        Status ==
        SingleInstanceStatus.PrimaryInstance;
}

public sealed class ActivationRequestedEventArgs :
    EventArgs
{
    public ActivationRequestedEventArgs(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        Arguments = arguments;
    }

    public IReadOnlyList<string> Arguments { get; }
}
