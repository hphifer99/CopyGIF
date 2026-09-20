using CopyGIF.Core.Models;

namespace CopyGIF.Application.Onboarding;

public interface IOnboardingCoordinator
{
    /// <summary>Every provider a person can pick in setup, in registration order.</summary>
    IReadOnlyList<OnboardingProviderOption> Providers { get; }

    /// <summary>The API key page of the default provider, or null when it has none.</summary>
    Uri? CredentialHelpUri { get; }

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

    public Uri? CredentialHelpUri { get; init; }

    public string? CredentialInstructions { get; init; }
}

/// <summary>One choice in the setup provider list.</summary>
public sealed record OnboardingProviderOption(
    string Id,
    string DisplayName,
    Uri? CredentialHelpUri,
    string? CredentialInstructions);
