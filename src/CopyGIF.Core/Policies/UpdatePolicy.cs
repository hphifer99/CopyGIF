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

        // A per-machine MSI can raise UAC. Never trigger that from the
        // background update timer, even if auto install was saved earlier.
        if (configuredMode == UpdateMode.DownloadAndInstall &&
            installationContext.Scope != InstallScope.CurrentUser)
            return UpdateMode.DownloadAndPrompt;

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
