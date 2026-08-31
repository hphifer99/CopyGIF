using CopyGIF.Application.Providers;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Application.Setup;

public sealed class ProviderSetupCoordinator :
    IProviderSetupCoordinator
{
    private readonly IActiveGifProviderAccessor
        _providerAccessor;

    private readonly IReadOnlyDictionary<
        string,
        IGifProviderCredentialManager>
        _credentialManagers;

    public ProviderSetupCoordinator(
        IActiveGifProviderAccessor providerAccessor,
        IEnumerable<
            IGifProviderCredentialManager>
            credentialManagers)
    {
        _providerAccessor =
            providerAccessor ??
            throw new ArgumentNullException(
                nameof(providerAccessor));

        ArgumentNullException.ThrowIfNull(
            credentialManagers);

        _credentialManagers =
            credentialManagers.ToDictionary(
                manager => manager.ProviderId,
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ProviderSetupState>
        GetStateAsync(
            CancellationToken cancellationToken =
                default)
    {
        IGifProvider provider =
            _providerAccessor
                .GetActiveProvider();

        IGifProviderCredentialManager manager =
            GetCredentialManager(
                provider.Id);

        bool hasCredential =
            await manager
                .HasCredentialAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return new ProviderSetupState
        {
            ProviderId =
                provider.Id,

            ProviderDisplayName =
                provider.DisplayName,

            HasCredential =
                hasCredential
        };
    }

    public async Task<CredentialValidationResult>
        ValidateAndSaveCredentialAsync(
            string credential,
            CancellationToken cancellationToken =
                default)
    {
        if (string.IsNullOrWhiteSpace(
                credential))
        {
            return CredentialValidationResult.Invalid(
                "An API key is required.");
        }

        IGifProvider provider =
            _providerAccessor
                .GetActiveProvider();

        IGifProviderCredentialManager manager =
            GetCredentialManager(
                provider.Id);

        CredentialValidationResult result =
            await manager
                .ValidateCredentialAsync(
                    credential.Trim(),
                    cancellationToken)
                .ConfigureAwait(false);

        if (!result.IsValid)
        {
            return result;
        }

        await manager
            .SaveCredentialAsync(
                credential.Trim(),
                cancellationToken)
            .ConfigureAwait(false);

        return result;
    }

    public Task ClearCredentialAsync(
        CancellationToken cancellationToken =
            default)
    {
        IGifProvider provider =
            _providerAccessor
                .GetActiveProvider();

        IGifProviderCredentialManager manager =
            GetCredentialManager(
                provider.Id);

        return manager
            .DeleteCredentialAsync(
                cancellationToken);
    }

    private IGifProviderCredentialManager
        GetCredentialManager(
            string providerId)
    {
        if (_credentialManagers.TryGetValue(
                providerId,
                out IGifProviderCredentialManager?
                    manager))
        {
            return manager;
        }

        throw new InvalidOperationException(
            $"No credential manager is registered for GIF provider '{providerId}'.");
    }
}