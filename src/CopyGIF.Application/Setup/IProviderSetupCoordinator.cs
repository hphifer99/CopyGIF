using CopyGIF.Core.Models;

namespace CopyGIF.Application.Setup;

public interface IProviderSetupCoordinator
{
    Task<ProviderSetupState> GetStateAsync(
        CancellationToken cancellationToken = default);

    Task<CredentialValidationResult>
        ValidateAndSaveCredentialAsync(
            string credential,
            CancellationToken cancellationToken = default);

    Task ClearCredentialAsync(
        CancellationToken cancellationToken = default);
}