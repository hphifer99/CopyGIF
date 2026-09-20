using CopyGIF.Application.Providers;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Credentials;

public sealed class ApiCredentialCoordinator :
    IApiCredentialCoordinator
{
    private readonly IActiveGifProviderAccessor
        _providerAccessor;

    private readonly ISettingsStore
        _settingsStore;

    private readonly Dictionary<
        string,
        IGifProviderCredentialManager>
        _credentialManagers;

    public ApiCredentialCoordinator(
        IActiveGifProviderAccessor providerAccessor,
        ISettingsStore settingsStore,
        IEnumerable<IGifProviderCredentialManager>
            credentialManagers)
    {
        _providerAccessor =
            providerAccessor ??
            throw new ArgumentNullException(
                nameof(providerAccessor));

        _settingsStore =
            settingsStore ??
            throw new ArgumentNullException(
                nameof(settingsStore));

        ArgumentNullException.ThrowIfNull(
            credentialManagers);

        _credentialManagers =
            CreateCredentialManagerDictionary(
                credentialManagers);
    }

    public async Task<ApiCredentialState>
        GetStateAsync(
            CancellationToken cancellationToken = default)
    {
        IGifProvider provider =
            await GetActiveProviderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        IGifProviderCredentialManager manager =
            GetCredentialManager(
                provider.Id);

        bool hasCredential =
            await manager
                .HasCredentialAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return new ApiCredentialState
        {
            ProviderId = provider.Id,
            ProviderDisplayName = provider.DisplayName,
            HasCredential = hasCredential
        };
    }

    public async Task<CredentialValidationResult>
        ValidateAndSaveAsync(
            string credential,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                credential))
        {
            return CredentialValidationResult.Invalid(
                "An API key is required.",
                CredentialValidationFailure.MissingCredential);
        }

        IGifProvider provider =
            await GetActiveProviderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        IGifProviderCredentialManager manager =
            GetCredentialManager(
                provider.Id);

        string normalizedCredential =
            credential.Trim();

        CredentialValidationResult result =
            await manager
                .ValidateCredentialAsync(
                    normalizedCredential,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!result.IsValid)
        {
            return result;
        }

        await manager
            .SaveCredentialAsync(
                normalizedCredential,
                cancellationToken)
            .ConfigureAwait(false);

        return result;
    }

    public async Task DeleteAsync(
        CancellationToken cancellationToken = default)
    {
        IGifProvider provider =
            await GetActiveProviderAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        IGifProviderCredentialManager manager =
            GetCredentialManager(
                provider.Id);

        await manager
            .DeleteCredentialAsync(
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IGifProvider>
        GetActiveProviderAsync(
            CancellationToken cancellationToken)
    {
        AppSettings settings =
            AppSettingsNormalizer.Normalize(
                await _settingsStore
                    .LoadAsync(
                        cancellationToken)
                    .ConfigureAwait(false));

        return _providerAccessor
            .GetActiveProvider(
                settings);
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

    private static Dictionary<
        string,
        IGifProviderCredentialManager>
        CreateCredentialManagerDictionary(
            IEnumerable<IGifProviderCredentialManager>
                credentialManagers)
    {
        Dictionary<
            string,
            IGifProviderCredentialManager> result =
                new(
                    StringComparer.OrdinalIgnoreCase);

        foreach (IGifProviderCredentialManager manager
                 in credentialManagers)
        {
            ArgumentNullException.ThrowIfNull(
                manager);

            ArgumentException.ThrowIfNullOrWhiteSpace(
                manager.ProviderId);

            if (!result.TryAdd(
                    manager.ProviderId,
                    manager))
            {
                throw new InvalidOperationException(
                    $"More than one credential manager is registered for GIF provider '{manager.ProviderId}'.");
            }
        }

        return result;
    }
}
