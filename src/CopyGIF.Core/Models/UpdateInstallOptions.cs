namespace CopyGIF.Core.Models;

/// <summary>
/// How a verified update package is handed to the installer.
/// The default value keeps the original behavior: start the installer and leave the
/// running application alone.
/// </summary>
public sealed record UpdateInstallOptions
{
    public static UpdateInstallOptions Interactive { get; } =
        new();

    /// <summary>
    /// Run the installer without a wizard or progress window. Windows may still show its
    /// administrator (UAC) prompt for a per-machine installation.
    /// </summary>
    public bool Silent { get; init; }

    /// <summary>
    /// The caller will exit the application right after the installer is handed off, and the
    /// installer starts the application again when it finishes (successfully or not, so the
    /// user is not left without CopyGIF). The installer package does this itself, no script or
    /// helper program is involved. It applies to a per-user installation only: an elevated
    /// installer would start CopyGIF with administrator rights, so a per-machine installation
    /// does not restart CopyGIF and the user opens it again.
    /// </summary>
    public bool RestartApplication { get; init; }

    /// <summary>
    /// Whether the package is checked against the certificate authority's revocation servers
    /// during the final check before the installer starts. See
    /// <see cref="UpdateVerificationOptions.CheckRevocationOnline"/>.
    /// </summary>
    public bool CheckRevocationOnline { get; init; } = true;

    /// <summary>
    /// Whether the installer must be started elevated (the Windows UAC prompt). This is true for
    /// a per-machine installation and false for a per-user one. The update coordinator sets it
    /// from the real installation scope, so callers normally leave it alone. The default is
    /// true because asking for elevation is the safe failure: an installer that needs it cannot
    /// then fail silently for lack of rights.
    /// </summary>
    public bool RequiresElevation { get; init; } = true;
}
