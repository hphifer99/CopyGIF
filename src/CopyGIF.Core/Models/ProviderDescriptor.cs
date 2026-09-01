namespace CopyGIF.Core.Models;

public sealed record ProviderDescriptor
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public ProviderCapabilities Capabilities { get; init; }

    public bool RequiresCredential { get; init; } = true;

    public string? AttributionText { get; init; }

    public Uri? AttributionUri { get; init; }

    public bool Supports(
        ProviderCapabilities capability)
    {
        return (
            Capabilities &
            capability) == capability;
    }
}
