namespace CopyGIF.Application.Setup;

public sealed record ProviderSetupState
{
    public required string ProviderId
    {
        get;
        init;
    }

    public required string ProviderDisplayName
    {
        get;
        init;
    }

    public required bool HasCredential
    {
        get;
        init;
    }
}