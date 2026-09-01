using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;
using CopyGIF.Core.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Core.Tests.Policies;

[TestClass]
public sealed class UpdatePolicyTests
{
    [TestMethod]
    public void UsesApplicationUpdater_Msi_ReturnsTrue()
    {
        InstallationContext context = new()
        {
            Channel = InstallChannel.Msi,
            Scope = InstallScope.CurrentUser
        };

        Assert.IsTrue(
            UpdatePolicy.UsesApplicationUpdater(
                context));
    }

    [TestMethod]
    public void UsesApplicationUpdater_StoreOrUnknown_ReturnsFalse()
    {
        InstallationContext storeContext = new()
        {
            Channel =
                InstallChannel.MicrosoftStore,

            Scope =
                InstallScope.CurrentUser
        };

        InstallationContext unknownContext =
            new();

        Assert.IsFalse(
            UpdatePolicy.UsesApplicationUpdater(
                storeContext));

        Assert.IsFalse(
            UpdatePolicy.UsesApplicationUpdater(
                unknownContext));
    }

    [TestMethod]
    public void ResolveMode_RecommendedCurrentUserMsi_InstallsAutomatically()
    {
        InstallationContext context = new()
        {
            Channel = InstallChannel.Msi,
            Scope = InstallScope.CurrentUser
        };

        UpdateMode result =
            UpdatePolicy.ResolveMode(
                UpdateMode.Recommended,
                context);

        Assert.AreEqual(
            UpdateMode.DownloadAndInstall,
            result);
    }

    [TestMethod]
    public void ResolveMode_RecommendedAllUsersMsi_PromptsBeforeInstallation()
    {
        InstallationContext context = new()
        {
            Channel = InstallChannel.Msi,
            Scope = InstallScope.AllUsers
        };

        UpdateMode result =
            UpdatePolicy.ResolveMode(
                UpdateMode.Recommended,
                context);

        Assert.AreEqual(
            UpdateMode.DownloadAndPrompt,
            result);
    }

    [TestMethod]
    public void ResolveMode_UnknownOrStoreContext_UsesSafePromptDefault()
    {
        InstallationContext unknownContext =
            new();

        InstallationContext storeContext = new()
        {
            Channel =
                InstallChannel.MicrosoftStore,

            Scope =
                InstallScope.CurrentUser
        };

        Assert.AreEqual(
            UpdateMode.DownloadAndPrompt,
            UpdatePolicy.ResolveMode(
                UpdateMode.Recommended,
                unknownContext));

        Assert.AreEqual(
            UpdateMode.DownloadAndPrompt,
            UpdatePolicy.ResolveMode(
                UpdateMode.Recommended,
                storeContext));
    }

    [TestMethod]
    public void ResolveMode_ExplicitMode_PreservesUserSelection()
    {
        InstallationContext context = new()
        {
            Channel = InstallChannel.Msi,
            Scope = InstallScope.AllUsers
        };

        Assert.AreEqual(
            UpdateMode.NotifyOnly,
            UpdatePolicy.ResolveMode(
                UpdateMode.NotifyOnly,
                context));

        Assert.AreEqual(
            UpdateMode.DownloadAndPrompt,
            UpdatePolicy.ResolveMode(
                UpdateMode.DownloadAndPrompt,
                context));

        Assert.AreEqual(
            UpdateMode.DownloadAndInstall,
            UpdatePolicy.ResolveMode(
                UpdateMode.DownloadAndInstall,
                context));
    }
}
