using CopyGIF.Application.Credentials;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;

namespace CopyGIF.Application.Onboarding;

public sealed class OnboardingCoordinator :
    IOnboardingCoordinator
{
    private static readonly Uri KlipyCredentialHelpUri =
        new(
            "https://klipy.com/developers");

    private readonly IApiCredentialCoordinator
        _credentialCoordinator;

    private readonly IUriLauncherService
        _uriLauncherService;

    public OnboardingCoordinator(
        IApiCredentialCoordinator credentialCoordinator,
        IUriLauncherService uriLauncherService)
    {
        _credentialCoordinator =
            credentialCoordinator ??
            throw new ArgumentNullException(
                nameof(credentialCoordinator));

        _uriLauncherService =
            uriLauncherService ??
            throw new ArgumentNullException(
                nameof(uriLauncherService));
    }

    public Uri CredentialHelpUri =>
        KlipyCredentialHelpUri;

    public async Task<OnboardingState> GetStateAsync(
        CancellationToken cancellationToken = default)
    {
        ApiCredentialState credentialState =
            await _credentialCoordinator
                .GetStateAsync(
                    cancellationToken)
                .ConfigureAwait(false);

        return new OnboardingState
        {
            IsRequired =
                !credentialState.HasCredential,

            ProviderId =
                credentialState.ProviderId,

            ProviderDisplayName =
                credentialState.ProviderDisplayName,

            CredentialHelpUri =
                CredentialHelpUri
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
        return _uriLauncherService
            .TryLaunchAsync(
                CredentialHelpUri,
                cancellationToken);
    }
}
