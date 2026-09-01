namespace CopyGIF.Core.Models;

public readonly record struct GifIdentity
{
    public GifIdentity(
        string providerId,
        string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            providerId);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            id);

        ProviderId = providerId.Trim();
        Id = id.Trim();
    }

    public string ProviderId { get; }

    public string Id { get; }

    public override string ToString()
    {
        return $"{ProviderId}:{Id}";
    }
}
