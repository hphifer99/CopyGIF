using CopyGIF.Core.Contracts;

namespace CopyGIF.Infrastructure.Time;

public sealed class SystemClock :
    IClock
{
    public DateTimeOffset UtcNow =>
        DateTimeOffset.UtcNow;

    public Task DelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        return Task.Delay(
            delay,
            cancellationToken);
    }
}
