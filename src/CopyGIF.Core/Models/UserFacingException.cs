namespace CopyGIF.Core.Models;

/// <summary>
/// An exception whose <see cref="Exception.Message"/> was written by CopyGIF for the person
/// using the app, so it can be shown as is. Every other exception message can contain file
/// paths, HRESULT text or other unlocalized detail and must not be shown directly.
/// </summary>
public sealed class UserFacingException : Exception
{
    public UserFacingException(string message)
        : base(message)
    {
    }

    public UserFacingException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
