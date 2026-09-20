using CopyGIF.Core.Settings;

namespace CopyGIF.Application.Updates;

public enum UpdateFollowUp
{
    None,

    /// <summary>CopyGIF is in the foreground: ask the user (install, not now, or skip this version).</summary>
    PromptUser,

    /// <summary>CopyGIF is hidden in the tray: install without an installer window and restart.</summary>
    InstallInBackground
}

/// <summary>
/// Decides what to do with an update that has been downloaded and verified.
/// The rule: never interrupt the user who is looking at CopyGIF without asking, and never
/// leave a verified update waiting when nobody is looking at CopyGIF.
/// </summary>
public static class UpdateHandoffPolicy
{
    public static UpdateFollowUp Decide(
        AutomaticUpdateResult result,
        bool isApplicationInForeground,
        bool alreadyHandledForThisVersion)
    {
        ArgumentNullException.ThrowIfNull(
            result);

        if (result.Action != AutomaticUpdateAction.Prompt ||
            result.Preparation is not
            {
                IsReady: true
            })
        {
            return UpdateFollowUp.None;
        }

        // The user already answered (not now) or an install was already attempted for this
        // version in this session. Do not ask again and do not raise another UAC prompt.
        if (alreadyHandledForThisVersion)
        {
            return UpdateFollowUp.None;
        }

        if (isApplicationInForeground)
        {
            return UpdateFollowUp.PromptUser;
        }

        // Hidden in the tray. Only install on its own if the user's update mode says to
        // (Recommended resolves to this for a per-user installation, which never raises UAC).
        // "Download and prompt" never installs without the user's answer.
        return result.Check.ResolvedMode == UpdateMode.DownloadAndInstall
            ? UpdateFollowUp.InstallInBackground
            : UpdateFollowUp.None;
    }
}
