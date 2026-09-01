using System.Security.Cryptography;
using System.Text;
using CopyGIF.Core.Contracts;

namespace CopyGIF.Platform.Windows.Secrets;

public sealed class DpapiLegacyCredentialDecoder :
    ILegacyCredentialDecoder
{
    private const int MaximumProtectedTextLength =
        128 * 1024;

    private static readonly UTF8Encoding StrictUtf8 =
        new(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

    public string DecodeCurrentUserCredential(
        string protectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            protectedValue);

        string normalized =
            protectedValue.Trim();

        if (normalized.Length >
            MaximumProtectedTextLength)
        {
            throw new InvalidDataException(
                "The legacy protected credential exceeds its maximum allowed size.");
        }

        byte[]? protectedBytes = null;
        byte[]? plainBytes = null;

        try
        {
            protectedBytes =
                Convert.FromBase64String(
                    normalized);

            plainBytes =
                ProtectedData.Unprotect(
                    protectedBytes,
                    optionalEntropy: null,
                    DataProtectionScope.CurrentUser);

            return StrictUtf8.GetString(
                plainBytes);
        }
        catch (Exception exception)
            when (exception is
                FormatException or
                CryptographicException or
                DecoderFallbackException)
        {
            throw new InvalidDataException(
                "The legacy protected credential could not be decoded for the current Windows user.",
                exception);
        }
        finally
        {
            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(
                    protectedBytes);
            }

            if (plainBytes is not null)
            {
                CryptographicOperations.ZeroMemory(
                    plainBytes);
            }
        }
    }
}
