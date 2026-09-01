using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Core.Policies;

public static class UpdatePolicy
{
    public static bool UsesApplicationUpdater(
        InstallationContext installationContext)
    {
        ArgumentNullException.ThrowIfNull(
            installationContext);

        return installationContext
            .UsesApplicationManagedUpdates;
    }

    public static UpdateMode ResolveMode(
        UpdateMode configuredMode,
        InstallationContext installationContext)
    {
        ArgumentNullException.ThrowIfNull(
            installationContext);

        if (configuredMode != UpdateMode.Recommended)
        {
            return configuredMode;
        }

        return installationContext.Channel ==
                   InstallChannel.Msi &&
               installationContext.Scope ==
                   InstallScope.CurrentUser
            ? UpdateMode.DownloadAndInstall
            : UpdateMode.DownloadAndPrompt;
    }
}
