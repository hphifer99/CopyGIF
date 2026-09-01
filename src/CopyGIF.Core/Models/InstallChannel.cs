namespace CopyGIF.Core.Models;

public enum InstallChannel
{
    None,
    MicrosoftStore,
    Msi
}

public enum InstallScope
{
    None,
    CurrentUser,
    AllUsers
}

public sealed record InstallationContext
{
    public InstallChannel Channel { get; init; }

    public InstallScope Scope { get; init; }

    public bool UsesApplicationManagedUpdates =>
        Channel == InstallChannel.Msi;
}
