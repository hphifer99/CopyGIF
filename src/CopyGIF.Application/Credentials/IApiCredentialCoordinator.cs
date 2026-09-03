using CopyGIF.Core.Models;

namespace CopyGIF.Application.Credentials;

public interface IApiCredentialCoordinator
{
    Task<ApiCredentialState> GetStateAsync(
        CancellationToken cancellationToken = default);

    Task<CredentialValidationResult> ValidateAndSaveAsync(
        string credential,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        CancellationToken cancellationToken = default);
}

public sealed record ApiCredentialState
{
    public required string ProviderId { get; init; }

    public required string ProviderDisplayName { get; init; }

    public required bool HasCredential { get; init; }
}
