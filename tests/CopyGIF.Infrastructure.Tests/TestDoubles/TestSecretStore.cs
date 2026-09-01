using CopyGIF.Core.Contracts;

namespace CopyGIF.Infrastructure.Tests.TestDoubles;

internal sealed class TestSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _values =
        new(StringComparer.Ordinal);

    public TestSecretStore()
    {
    }

    public TestSecretStore(
        string name,
        string value)
    {
        _values[name] = value;
    }

    public Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _values.TryGetValue(
            name,
            out string? value);

        return Task.FromResult(value);
    }

    public Task SetAsync(
        string name,
        string value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _values[name] = value;

        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _values.Remove(name);

        return Task.CompletedTask;
    }
}