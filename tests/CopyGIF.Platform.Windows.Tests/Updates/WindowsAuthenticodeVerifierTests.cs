using CopyGIF.Platform.Windows.Updates;

namespace CopyGIF.Platform.Windows.Tests.Updates;

[TestClass]
public sealed class WindowsAuthenticodeVerifierTests
{
    [TestMethod]
    public void ClassifyTrustResult_Success_IsTrusted()
    {
        Assert.AreEqual(
            AuthenticodeVerificationStatus.Trusted,
            WindowsAuthenticodeVerifier.ClassifyTrustResult(
                0));
    }

    // CRYPT_E_REVOCATION_OFFLINE specifically identifies an unavailable revocation server.
    [TestMethod]
    [DataRow(unchecked((int)0x80092013))]
    public void ClassifyTrustResult_RevocationServerOffline_IsRevocationUnavailable(
        int trustResult)
    {
        Assert.AreEqual(
            AuthenticodeVerificationStatus.RevocationUnavailable,
            WindowsAuthenticodeVerifier.ClassifyTrustResult(
                trustResult));
    }

    // TRUST_E_NOSIGNATURE, TRUST_E_BAD_DIGEST, CERT_E_UNTRUSTEDROOT, CERT_E_EXPIRED,
    // CERT_E_REVOKED and a generic failure must all still fail the package.
    [TestMethod]
    [DataRow(unchecked((int)0x800B0100))]
    [DataRow(unchecked((int)0x80096010))]
    [DataRow(unchecked((int)0x800B0109))]
    [DataRow(unchecked((int)0x800B0101))]
    [DataRow(unchecked((int)0x800B010C))]
    [DataRow(unchecked((int)0x80092012))]
    [DataRow(unchecked((int)0x800B010E))]
    [DataRow(1)]
    [DataRow(-1)]
    public void ClassifyTrustResult_AnyOtherFailure_IsAnInvalidSignature(
        int trustResult)
    {
        Assert.AreEqual(
            AuthenticodeVerificationStatus.InvalidSignature,
            WindowsAuthenticodeVerifier.ClassifyTrustResult(
                trustResult));
    }
}
