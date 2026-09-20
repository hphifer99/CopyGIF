using System.Security.Cryptography;
using System.Text;
using CopyGIF.Platform.Windows.Secrets;

namespace CopyGIF.Platform.Windows.Tests.Secrets;

[TestClass]
public sealed class DpapiLegacyCredentialDecoderTests
{
    [TestMethod]
    public void DecodeCurrentUserCredential_V1ProtectedValue_ReturnsPlaintext()
    {
        string expected =
            "legacy-key-" +
            Guid.NewGuid().ToString("N");

        byte[] plainBytes =
            Encoding.UTF8.GetBytes(expected);

        byte[] protectedBytes =
            ProtectedData.Protect(
                plainBytes,
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);

        try
        {
            string protectedValue =
                Convert.ToBase64String(
                    protectedBytes);

            DpapiLegacyCredentialDecoder decoder =
                new();

            string actual =
                decoder.DecodeCurrentUserCredential(
                    "  " + protectedValue + "  ");

            Assert.AreEqual(
                expected,
                actual);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                plainBytes);

            CryptographicOperations.ZeroMemory(
                protectedBytes);
        }
    }

    [TestMethod]
    public void DecodeCurrentUserCredential_InvalidBase64_UsesSanitizedError()
    {
        const string invalidValue =
            "not-a-protected-secret!";

        DpapiLegacyCredentialDecoder decoder =
            new();

        InvalidDataException exception =
            Assert.ThrowsExactly<InvalidDataException>(
                () =>
                    decoder.DecodeCurrentUserCredential(
                        invalidValue));

        Assert.IsFalse(
            exception.Message.Contains(
                invalidValue,
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void DecodeCurrentUserCredential_ValueWithDifferentEntropy_IsRejected()
    {
        byte[] plainBytes =
            Encoding.UTF8.GetBytes(
                "legacy-test-value");

        byte[] entropy =
            Encoding.UTF8.GetBytes(
                "different-entropy");

        byte[] protectedBytes =
            ProtectedData.Protect(
                plainBytes,
                entropy,
                DataProtectionScope.CurrentUser);

        try
        {
            string protectedValue =
                Convert.ToBase64String(
                    protectedBytes);

            DpapiLegacyCredentialDecoder decoder =
                new();

            Assert.ThrowsExactly<InvalidDataException>(
                () =>
                    decoder.DecodeCurrentUserCredential(
                        protectedValue));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                plainBytes);

            CryptographicOperations.ZeroMemory(
                entropy);

            CryptographicOperations.ZeroMemory(
                protectedBytes);
        }
    }
}
