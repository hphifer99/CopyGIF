using CopyGIF.Core.Models;

namespace CopyGIF.Application.Onboarding;

public interface IOnboardingCoordinator
{
    Uri CredentialHelpUri { get; }

    Task<OnboardingState> GetStateAsync(
        CancellationToken cancellationToken = default);

    Task<CredentialValidationResult> CompleteAsync(
        string credential,
        CancellationToken cancellationToken = default);

    Task<bool> OpenCredentialHelpAsync(
        CancellationToken cancellationToken = default);
}

public sealed record OnboardingState
{
    public required bool IsRequired { get; init; }

    public required string ProviderId { get; init; }

    public required string ProviderDisplayName { get; init; }

    public required Uri CredentialHelpUri { get; init; }
}
