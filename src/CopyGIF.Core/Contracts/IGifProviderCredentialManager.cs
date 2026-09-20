using CopyGIF.Core.Models;

namespace CopyGIF.Core.Contracts;

public interface IGifProviderCredentialManager
{
    string ProviderId { get; }

    string DisplayName { get; }

    /// <summary>
    /// The name under which this provider's API key is kept in the secret store. It is stable
    /// across releases, because changing it would make saved keys unreachable.
    /// </summary>
    string SecretName { get; }

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