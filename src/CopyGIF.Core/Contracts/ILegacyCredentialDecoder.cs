namespace CopyGIF.Core.Contracts;

public interface ILegacyCredentialDecoder
{
    string DecodeCurrentUserCredential(
        string protectedValue);
}
