using CopyGIF.Core.Contracts;

namespace CopyGIF.Testing;

public sealed class FakeClock :
    IClock
{
    private readonly object _syncRoot = new();

    private readonly List<TimeSpan>
        _delayRequests = [];

    public FakeClock()
        : this(
            new DateTimeOffset(
                2026,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero))
    {
    }

    public FakeClock(
        DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; set; }

    public Func<
        TimeSpan,
        CancellationToken,
        Task>? DelayHandler
    { get; set; }

    public IReadOnlyList<TimeSpan>
        DelayRequests
    {
        get
        {
            lock (_syncRoot)
            {
                return _delayRequests.ToArray();
            }
        }
    }

    public Task DelayAsync(
        TimeSpan delay,
        CancellationToken cancellationToken = default)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(delay),
                delay,
                "The delay cannot be negative.");
        }

        lock (_syncRoot)
        {
            _delayRequests.Add(
                delay);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return DelayHandler is null
            ? Task.CompletedTask
            : DelayHandler(
                delay,
                cancellationToken);
    }

    public void Advance(
        TimeSpan duration)
    {
        UtcNow = UtcNow.Add(
            duration);
    }
}
