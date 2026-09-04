using CopyGIF.Application.Onboarding;
using CopyGIF.Core.Models;
using CopyGIF.Presentation.Common;
using CopyGIF.Presentation.Onboarding;

namespace CopyGIF.Presentation.Tests.Onboarding;

[TestClass]
public sealed class OnboardingViewModelTests
{
    [TestMethod]
    public async Task LoadCommand_WhenCredentialMissing_RequiresSetup()
    {
        FakeOnboardingCoordinator coordinator =
            new()
            {
                State =
                    new OnboardingState
                    {
                        IsRequired =
                            true,

                        ProviderId =
                            "klipy",

                        ProviderDisplayName =
                            "KLIPY",

                        CredentialHelpUri =
                            new Uri(
                                "https://klipy.com/developers")
                    }
            };

        OnboardingViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsLoaded);

        Assert.IsTrue(
            viewModel.IsRequired);

        Assert.IsFalse(
            viewModel.IsCompleted);

        Assert.AreEqual(
            "klipy",
            viewModel.ProviderId);

        Assert.AreEqual(
            "KLIPY",
            viewModel.ProviderDisplayName);

        Assert.AreEqual(
            coordinator.CredentialHelpUri,
            viewModel.CredentialHelpUri);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);

        Assert.IsNotNull(
            viewModel.Message);
    }

    [TestMethod]
    public async Task LoadCommand_WhenCredentialExists_IsComplete()
    {
        FakeOnboardingCoordinator coordinator =
            new()
            {
                State =
                    new OnboardingState
                    {
                        IsRequired =
                            false,

                        ProviderId =
                            "klipy",

                        ProviderDisplayName =
                            "KLIPY",

                        CredentialHelpUri =
                            new Uri(
                                "https://klipy.com/developers")
                    }
            };

        OnboardingViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsLoaded);

        Assert.IsFalse(
            viewModel.IsRequired);

        Assert.IsTrue(
            viewModel.IsCompleted);

        Assert.IsNull(
            viewModel.Message);
    }

    [TestMethod]
    public async Task CompleteCommand_ValidCredential_CompletesSetup()
    {
        FakeOnboardingCoordinator coordinator =
            CreateRequiredCoordinator();

        OnboardingViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.Credential =
            "  valid-key  ";

        Assert.IsTrue(
            viewModel.CompleteCommand
                .CanExecute(null));

        await viewModel
            .CompleteCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            "valid-key",
            coordinator.LastCredential);

        Assert.IsFalse(
            viewModel.IsRequired);

        Assert.IsTrue(
            viewModel.IsCompleted);

        Assert.AreEqual(
            string.Empty,
            viewModel.Credential);

        Assert.IsNotNull(
            viewModel.Message);

        Assert.AreEqual(
            UserMessageSeverity.Success,
            viewModel.Message.Severity);
    }

    [TestMethod]
    public async Task CompleteCommand_InvalidCredential_KeepsSetupRequired()
    {
        FakeOnboardingCoordinator coordinator =
            CreateRequiredCoordinator();

        coordinator.CompletionResult =
            CredentialValidationResult.Invalid(
                "That API key is invalid.",
                CredentialValidationFailure.InvalidCredential);

        OnboardingViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        viewModel.Credential =
            "bad-key";

        await viewModel
            .CompleteCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.IsRequired);

        Assert.IsFalse(
            viewModel.IsCompleted);

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
    public async Task CompleteCommand_IsDisabledUntilLoaded()
    {
        OnboardingViewModel viewModel =
            new(
                CreateRequiredCoordinator());

        viewModel.Credential =
            "key";

        Assert.IsFalse(
            viewModel.CompleteCommand
                .CanExecute(null));

        await viewModel
            .LoadCommand
            .ExecuteAsync(null);

        Assert.IsTrue(
            viewModel.CompleteCommand
                .CanExecute(null));
    }

    [TestMethod]
    public async Task OpenCredentialHelpCommand_UsesCoordinator()
    {
        FakeOnboardingCoordinator coordinator =
            CreateRequiredCoordinator();

        OnboardingViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .OpenCredentialHelpCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            1,
            coordinator.OpenHelpCount);

        Assert.AreEqual(
            AsyncOperationStatus.Succeeded,
            viewModel.OperationState.Status);
    }

    [TestMethod]
    public async Task OpenCredentialHelpCommand_WhenLaunchFails_ShowsWarning()
    {
        FakeOnboardingCoordinator coordinator =
            CreateRequiredCoordinator();

        coordinator.OpenHelpResult =
            false;

        OnboardingViewModel viewModel =
            new(
                coordinator);

        await viewModel
            .OpenCredentialHelpCommand
            .ExecuteAsync(null);

        Assert.AreEqual(
            AsyncOperationStatus.Failed,
            viewModel.OperationState.Status);

        Assert.IsNotNull(
            viewModel.Message);

        Assert.AreEqual(
            UserMessageSeverity.Warning,
            viewModel.Message.Severity);

        Assert.AreEqual(
            "credential_help_failed",
            viewModel.Message.Code);
    }

    private static FakeOnboardingCoordinator
        CreateRequiredCoordinator()
    {
        return new FakeOnboardingCoordinator
        {
            State =
                new OnboardingState
                {
                    IsRequired =
                        true,

                    ProviderId =
                        "klipy",

                    ProviderDisplayName =
                        "KLIPY",

                    CredentialHelpUri =
                        new Uri(
                            "https://klipy.com/developers")
                }
        };
    }

    private sealed class FakeOnboardingCoordinator :
        IOnboardingCoordinator
    {
        public Uri CredentialHelpUri
        { get; } =
            new(
                "https://klipy.com/developers");

        public OnboardingState State
        {
            get;
            init;
        } =
            new()
            {
                IsRequired =
                    true,

                ProviderId =
                    "klipy",

                ProviderDisplayName =
                    "KLIPY",

                CredentialHelpUri =
                    new Uri(
                        "https://klipy.com/developers")
            };

        public CredentialValidationResult CompletionResult
        {
            get;
            set;
        } =
            CredentialValidationResult.Valid();

        public bool OpenHelpResult
        {
            get;
            set;
        } =
            true;

        public string? LastCredential
        {
            get;
            private set;
        }

        public int OpenHelpCount
        {
            get;
            private set;
        }

        public Task<OnboardingState> GetStateAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            return Task.FromResult(
                State);
        }

        public Task<CredentialValidationResult> CompleteAsync(
            string credential,
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            LastCredential =
                credential;

            return Task.FromResult(
                CompletionResult);
        }

        public Task<bool> OpenCredentialHelpAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            OpenHelpCount++;

            return Task.FromResult(
                OpenHelpResult);
        }
    }
}
