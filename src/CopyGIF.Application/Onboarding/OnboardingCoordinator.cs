using System.Collections.ObjectModel;
using CopyGIF.Application.Credentials;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Onboarding;

public sealed class OnboardingCoordinator :
    IOnboardingCoordinator
{
    private readonly IApiCredentialCoordinator
        _credentialCoordinator;

    private readonly IUriLauncherService
        _uriLauncherService;

    private readonly ReadOnlyCollection<OnboardingProviderOption>
        _providers;

    public OnboardingCoordinator(
        IApiCredentialCoordinator credentialCoordinator,
        IUriLauncherService uriLauncherService,
        IProviderCatalog providerCatalog)
    {
        _credentialCoordinator =
            credentialCoordinator ??
            throw new ArgumentNullException(
                nameof(credentialCoordinator));

        _uriLauncherService =
            uriLauncherService ??
            throw new ArgumentNullException(
                nameof(uriLauncherService));

        ArgumentNullException.ThrowIfNull(
            providerCatalog);

        // Only providers that need a key belong in setup. The list follows the order in which
        // the providers were registered, so the first one is the natural default.
        _providers =
            Array.AsReadOnly(
                providerCatalog.Providers
                    .Where(provider => provider.RequiresCredential)
                    .Select(ToOption)
                    .ToArray());
    }

    public IReadOnlyList<OnboardingProviderOption> Providers =>
        _providers;

    public Uri? CredentialHelpUri =>
        (_providers.FirstOrDefault(
             option => string.Equals(
                 option.Id,
                 AppSettings.DefaultProviderId,
                 StringComparison.OrdinalIgnoreCase)) ??
         (_providers.Count > 0
             ? _providers[0]
             : null))?
        .CredentialHelpUri;

    public async Task<OnboardingState> GetStateAsync(
        CancellationToken cancellationToken = default)
    {
        ApiCredentialState credentialState =
            await _credentialCoordinator
                .GetStateAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        OnboardingProviderOption? option =
            _providers.FirstOrDefault(
                candidate => string.Equals(
                    candidate.Id,
                    credentialState.ProviderId,
                    StringComparison.OrdinalIgnoreCase));

        return new OnboardingState
        {
            IsRequired =
                !credentialState.HasCredential,

            ProviderId =
                credentialState.ProviderId,

            ProviderDisplayName =
                credentialState.ProviderDisplayName,

            CredentialHelpUri =
                option?.CredentialHelpUri,

            CredentialInstructions =
                option?.CredentialInstructions
        };
    }

    public Task<CredentialValidationResult> CompleteAsync(
        string credential,
        CancellationToken cancellationToken = default)
    {
        return _credentialCoordinator
            .ValidateAndSaveAsync(
                credential,
                cancellationToken);
    }

    public Task<bool> OpenCredentialHelpAsync(
        CancellationToken cancellationToken = default)
    {
        Uri? helpUri =
            CredentialHelpUri;

        return helpUri is null
            ? Task.FromResult(false)
            : _uriLauncherService
                .TryLaunchAsync(
                    helpUri,
                    cancellationToken);
    }

    private static OnboardingProviderOption ToOption(
        ProviderDescriptor provider)
    {
        return new OnboardingProviderOption(
            provider.Id,
            provider.DisplayName,
            provider.CredentialHelpUri,
            provider.CredentialInstructions);
    }
}
