using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IGifProviderCredentialManager
{
    string ProviderId { get; }

    string DisplayName { get; }

    Task<bool> HasCredentialAsync(
        CancellationToken cancellationToken = default);

    Task<CredentialValidationResult>
        ValidateCredentialAsync(
            string credential,
            CancellationToken cancellationToken = default);

    Task SaveCredentialAsync(
        string credential,
        CancellationToken cancellationToken = default);

    Task DeleteCredentialAsync(
        CancellationToken cancellationToken = default);
}