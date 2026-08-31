namespace CopyGIF.Core.Contracts;

public interface ISecretStore
{
    Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        string name,
        string value,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string name,
        CancellationToken cancellationToken = default);
}