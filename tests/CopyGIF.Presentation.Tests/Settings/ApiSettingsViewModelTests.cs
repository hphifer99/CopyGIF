using CopyGIF.Application.Credentials;
using CopyGIF.Application.Onboarding;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Settings;

namespace CopyGIF.Presentation.Tests.Settings;

[TestClass]
public sealed class ApiSettingsViewModelTests
{
    [TestMethod]
    public async Task LoadCommand_LoadsCredentialStateWithoutSecret()
    {
        FakeCredentialCoordinator credentials =
            new()
            {
                State =
                    new ApiCredentialState
                    {
                        ProviderId =
                            "klipy",

                        ProviderDisplayName =
                            "KLIPY",

                        HasCredential =
                            true
                    }
            };

        ApiSettingsViewModel viewModel =
            CreateViewModel(
                credentials);

        viewModel.Credential =
            "temporary-value";

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsLoaded);

        Assert.IsTrue(
            viewModel.HasCredential);

        Assert.AreEqual(
            "klipy",
            viewModel.ProviderId);

        Assert.AreEqual(
            "KLIPY",
            viewModel.ProviderDisplayName);

        Assert.AreEqual(
            string.Empty,
            viewModel.Credential);

        Assert.AreEqual(
            "API key configured",
            viewModel.CredentialStatusText);

        Assert.IsTrue(
            viewModel.DeleteCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task SaveCommand_ValidCredential_SavesAndClearsInput()
    {
        FakeCredentialCoordinator credentials =
            new();

        ApiSettingsViewModel viewModel =
            CreateViewModel(
                credentials);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.Credential =
            "  new-key  ";

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            "new-key",
            credentials.LastSavedCredential);

        Assert.IsTrue(
            viewModel.HasCredential);

        Assert.AreEqual(
            string.Empty,
            viewModel.Credential);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);

        Assert.IsNotNull(
            viewModel.Message);

        Assert.AreEqual(
            UserMessageSeverity.Success,
            viewModel.Message.Severity);
    }

    [TestMethod]
    public async Task SaveCommand_InvalidCredential_DoesNotMarkConfigured()
    {
        FakeCredentialCoordinator credentials =
            new()
            {
                ValidationResult =
                    CredentialValidationResult.Invalid(
                        "Invalid API key.",
                        CredentialValidationFailure.InvalidCredential)
            };

        ApiSettingsViewModel viewModel =
            CreateViewModel(
                credentials);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.Credential =
            "bad-key";

        await viewModel
            .SaveCommand
            .ExecuteAsync(null);

        Assert.IsFalse(
            viewModel.HasCredential);

        Assert.AreEqual(
            "bad-key",
            viewModel.Credential);

        Assert.AreEqual(
            AsyncOperationStatus.Failed,
            viewModel.OperationState.Status);

        Assert.IsNotNull(
            viewModel.Message);

        Assert.AreEqual(
            "credential_invalid",
            viewModel.Message.Code);
    }

    [TestMethod]
    public async Task DeleteCommand_RemovesCredential()
    {
        FakeCredentialCoordinator credentials =
            new()
            {
                State =
                    new ApiCredentialState
                    {
                        ProviderId =
                            "klipy",

                        ProviderDisplayName =
                            "KLIPY",

                        HasCredential =
                            true
                    }
            };

        ApiSettingsViewModel viewModel =
            CreateViewModel(
                credentials);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        await viewModel
            .DeleteCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            credentials.DeleteCount);

        Assert.IsFalse(
            viewModel.HasCredential);

        Assert.IsFalse(
            viewModel.DeleteCommand
                .CanExecute(null));

        Assert.AreEqual(
            "API key not configured",
            viewModel.CredentialStatusText);
    }

    [TestMethod]
    public async Task SaveCommand_IsDisabledForBlankInput()
    {
        ApiSettingsViewModel viewModel =
            CreateViewModel(
                new FakeCredentialCoordinator());

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsFalse(
            viewModel.SaveCommand
                .CanExecute(null));

        viewModel.Credential =
            "key";

        Assert.IsTrue(
            viewModel.SaveCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task OpenCredentialHelpCommand_UsesExistingLauncher()
    {
        FakeOnboardingCoordinator onboarding =
            new();

        ApiSettingsViewModel viewModel =
            CreateViewModel(
                new FakeCredentialCoordinator(),
                onboarding);

        await viewModel
            .OpenCredentialHelpCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            onboarding.OpenHelpCount);

        Assert.AreEqual(
            onboarding.CredentialHelpUri,
            viewModel.CredentialHelpUri);
    }

    private static ApiSettingsViewModel CreateViewModel(
        FakeCredentialCoordinator credentialCoordinator,
        FakeOnboardingCoordinator? onboardingCoordinator = null)
    {
        return new ApiSettingsViewModel(
            credentialCoordinator,
            onboardingCoordinator ??
                new FakeOnboardingCoordinator());
    }

    private sealed class FakeCredentialCoordinator :
        IApiCredentialCoordinator
    {
        public ApiCredentialState State
        {
            get;
            init;
        } =
            new()
            {
                ProviderId =
                    "klipy",

                ProviderDisplayName =
                    "KLIPY",

                HasCredential =
                    false
            };

        public CredentialValidationResult ValidationResult
        {
            get;
            init;
        } =
            CredentialValidationResult.Valid();

        public string? LastSavedCredential
        {
            get;
            private set;
        }

        public int DeleteCount
        {
            get;
            private set;
        }

        public Task<ApiCredentialState> GetStateAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                State);
        }

        public Task<CredentialValidationResult> ValidateAndSaveAsync(
            string credential,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastSavedCredential =
                credential;

            return Task.FromResult(
                ValidationResult);
        }

        public Task DeleteAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            DeleteCount++;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeOnboardingCoordinator :
        IOnboardingCoordinator
    {
        public Uri CredentialHelpUri
        { get; } =
            new(
                "https://klipy.com/developers");

        public int OpenHelpCount
        {
            get;
            private set;
        }

        public Task<OnboardingState> GetStateAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new OnboardingState
                {
                    IsRequired =
                        true,

                    ProviderId =
                        "klipy",

                    ProviderDisplayName =
                        "KLIPY",

                    CredentialHelpUri =
                        CredentialHelpUri
                });
        }

        public Task<CredentialValidationResult> CompleteAsync(
            string credential,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                CredentialValidationResult.Valid());
        }

        public Task<bool> OpenCredentialHelpAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            OpenHelpCount++;

            return Task.FromResult(
                true);
        }
    }
}
