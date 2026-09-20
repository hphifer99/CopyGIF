using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CopyGIF.App.Services;

public enum UpdatePromptChoice
{
    InstallNow,
    RemindLater,
    SkipVersion
}

public static class UpdateInstallPrompt
{
    // Must be called on the UI thread with an element that is in a live window.
    // installsAtNextStart: choosing "Not now" makes CopyGIF install the update by itself the
    // next time it starts. That is only true for a per-user installation, which needs no
    // administrator prompt. For any other installation "Not now" only means "ask me later",
    // and the text says so, so the dialog never promises something that will not happen.
    public static async Task<UpdatePromptChoice> ShowAsync(
        FrameworkElement host,
        string version,
        bool installsAtNextStart)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        ContentDialog dialog = new()
        {
            XamlRoot = host.XamlRoot,
            RequestedTheme = host.ActualTheme,
            Title = "CopyGIF update ready",
            Content = installsAtNextStart
                ? $"CopyGIF {version} has been downloaded and verified. CopyGIF needs to restart to install it. If you choose Not now, CopyGIF installs it automatically the next time it starts."
                : $"CopyGIF {version} has been downloaded and verified. CopyGIF needs to restart to install it. If you choose Not now, CopyGIF asks you again later.",
            PrimaryButtonText = "Restart and update",
            SecondaryButtonText = "Skip this version",
            CloseButtonText = "Not now",
            DefaultButton = ContentDialogButton.Primary
        };

        ContentDialogResult result = await dialog.ShowAsync();

        return result switch
        {
            ContentDialogResult.Primary => UpdatePromptChoice.InstallNow,
            ContentDialogResult.Secondary => UpdatePromptChoice.SkipVersion,
            _ => UpdatePromptChoice.RemindLater
        };
    }
}
