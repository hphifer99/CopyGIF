using CopyGIF.Application.Updates;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Tests.Updates;

[TestClass]
public sealed class UpdateHandoffPolicyTests
{
    [TestMethod]
    public void Decide_PreparedUpdateWhileForeground_PromptsTheUser()
    {
        Assert.AreEqual(
            UpdateFollowUp.PromptUser,
            UpdateHandoffPolicy.Decide(
                CreateResult(
                    AutomaticUpdateAction.Prompt,
                    ready: true),
                isApplicationInForeground: true,
                alreadyHandledForThisVersion: false));
    }

    [TestMethod]
    public void Decide_PreparedUpdateWhileInTheTray_InstallsInTheBackground()
    {
        Assert.AreEqual(
            UpdateFollowUp.InstallInBackground,
            UpdateHandoffPolicy.Decide(
                CreateResult(
                    AutomaticUpdateAction.Prompt,
                    ready: true),
                isApplicationInForeground: false,
                alreadyHandledForThisVersion: false));
    }

    [TestMethod]
    public void Decide_DownloadAndPromptWhileInTheTray_NeverInstallsOnItsOwn()
    {
        Assert.AreEqual(
            UpdateFollowUp.None,
            UpdateHandoffPolicy.Decide(
                CreateResult(
                    AutomaticUpdateAction.Prompt,
                    ready: true,
                    UpdateMode.DownloadAndPrompt),
                isApplicationInForeground: false,
                alreadyHandledForThisVersion: false));
    }

    [TestMethod]
    [DataRow(UpdateMode.DownloadAndPrompt)]
    [DataRow(UpdateMode.DownloadAndInstall)]
    public void Decide_PreparedUpdateWhileForeground_AlwaysAsksWhateverTheMode(
        UpdateMode mode)
    {
        Assert.AreEqual(
            UpdateFollowUp.PromptUser,
            UpdateHandoffPolicy.Decide(
                CreateResult(
                    AutomaticUpdateAction.Prompt,
                    ready: true,
                    mode),
                isApplicationInForeground: true,
                alreadyHandledForThisVersion: false));
    }

    [TestMethod]
    public void Decide_AlreadyHandledThisSession_DoesNothing()
    {
        Assert.AreEqual(
            UpdateFollowUp.None,
            UpdateHandoffPolicy.Decide(
                CreateResult(
                    AutomaticUpdateAction.Prompt,
                    ready: true),
                isApplicationInForeground: false,
                alreadyHandledForThisVersion: true));

        Assert.AreEqual(
            UpdateFollowUp.None,
            UpdateHandoffPolicy.Decide(
                CreateResult(
                    AutomaticUpdateAction.Prompt,
                    ready: true),
                isApplicationInForeground: true,
                alreadyHandledForThisVersion: true));
    }

    [TestMethod]
    [DataRow(AutomaticUpdateAction.None)]
    [DataRow(AutomaticUpdateAction.Notify)]
    [DataRow(AutomaticUpdateAction.Installed)]
    [DataRow(AutomaticUpdateAction.VerificationFailed)]
    public void Decide_AnyActionOtherThanPrompt_DoesNothing(
        AutomaticUpdateAction action)
    {
        Assert.AreEqual(
            UpdateFollowUp.None,
            UpdateHandoffPolicy.Decide(
                CreateResult(
                    action,
                    ready: true),
                isApplicationInForeground: false,
                alreadyHandledForThisVersion: false));
    }

    [TestMethod]
    public void Decide_PromptWithoutAReadyPackage_DoesNothing()
    {
        Assert.AreEqual(
            UpdateFollowUp.None,
            UpdateHandoffPolicy.Decide(
                CreateResult(
                    AutomaticUpdateAction.Prompt,
                    ready: false),
                isApplicationInForeground: false,
                alreadyHandledForThisVersion: false));
    }

    private static AutomaticUpdateResult CreateResult(
        AutomaticUpdateAction action,
        bool ready,
        UpdateMode resolvedMode = UpdateMode.DownloadAndInstall)
    {
        UpdateManifest manifest =
            new()
            {
                Version = "2.1.0",
                Channel = "stable",
                AssetName = "CopyGIF-2.1.0-win-x64.msi",
                AssetUri =
                    new Uri(
                        "https://github.com/hphifer99/CopyGIF/releases/download/v2.1.0/CopyGIF-2.1.0-win-x64.msi"),
                SizeBytes = 1024,
                Sha256 =
                    new string(
                        'a',
                        64),
                MinimumSupportedVersion = "2.0.0",
                ReleaseNotesUri =
                    new Uri(
                        "https://github.com/hphifer99/CopyGIF/releases/tag/v2.1.0"),
                PublishedAtUtc =
                    new DateTimeOffset(
                        2026,
                        9,
                        3,
                        12,
                        0,
                        0,
                        TimeSpan.Zero)
            };

        DownloadedUpdatePackage package =
            new()
            {
                Manifest = manifest,
                FilePath = "CopyGIF-2.1.0-win-x64.msi",
                SizeBytes = manifest.SizeBytes,
                Sha256 = manifest.Sha256,
                DownloadedAtUtc = manifest.PublishedAtUtc
            };

        return new AutomaticUpdateResult
        {
            Action = action,
            Check =
                new UpdateCheckResult
                {
                    Status = UpdateCheckStatus.UpdateAvailable,
                    ResolvedMode = resolvedMode,
                    Installation =
                        new InstallationContext(),
                    State = new UpdateState()
                },
            Preparation =
                new UpdatePreparationResult
                {
                    Status =
                        ready
                            ? UpdatePreparationStatus.Ready
                            : UpdatePreparationStatus.VerificationFailed,
                    Verification =
                        ready
                            ? UpdatePackageVerificationResult.Valid()
                            : UpdatePackageVerificationResult.Invalid(
                                UpdatePackageVerificationFailure.Unknown,
                                "Failed."),
                    Package = package
                }
        };
    }
}
