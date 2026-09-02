using CopyGIF.Platform.Windows.Updates;

namespace CopyGIF.Platform.Windows.Tests.Updates;

[TestClass]
public sealed class
    CurrentExecutablePublisherTrustPolicyTests
{
    [TestMethod]
    public void IsTrustedPublisher_MatchingIdentity_ReturnsTrue()
    {
        FakePublisherReader reader = new();

        reader.Publishers["CopyGIF.exe"] =
            new AuthenticodePublisherIdentity(
                "CN=CopyGIF",
                "CN=Trusted Issuer");

        reader.Publishers["CopyGIF.msi"] =
            new AuthenticodePublisherIdentity(
                "CN=CopyGIF",
                "CN=Trusted Issuer");

        CurrentExecutablePublisherTrustPolicy policy =
            new(
                reader,
                () => "CopyGIF.exe");

        Assert.IsTrue(
            policy.IsTrustedPublisher(
                "CopyGIF.msi"));
    }

    [TestMethod]
    public void IsTrustedPublisher_DifferentSubject_ReturnsFalse()
    {
        FakePublisherReader reader = new();

        reader.Publishers["CopyGIF.exe"] =
            new AuthenticodePublisherIdentity(
                "CN=CopyGIF",
                "CN=Trusted Issuer");

        reader.Publishers["CopyGIF.msi"] =
            new AuthenticodePublisherIdentity(
                "CN=Unexpected Publisher",
                "CN=Trusted Issuer");

        CurrentExecutablePublisherTrustPolicy policy =
            new(
                reader,
                () => "CopyGIF.exe");

        Assert.IsFalse(
            policy.IsTrustedPublisher(
                "CopyGIF.msi"));
    }

    [TestMethod]
    public void IsTrustedPublisher_UnsignedApplication_ReturnsFalse()
    {
        FakePublisherReader reader = new();

        reader.Publishers["CopyGIF.msi"] =
            new AuthenticodePublisherIdentity(
                "CN=CopyGIF",
                "CN=Trusted Issuer");

        CurrentExecutablePublisherTrustPolicy policy =
            new(
                reader,
                () => "CopyGIF.exe");

        Assert.IsFalse(
            policy.IsTrustedPublisher(
                "CopyGIF.msi"));
    }

    private sealed class FakePublisherReader :
        ISignedFilePublisherReader
    {
        public Dictionary<
            string,
            AuthenticodePublisherIdentity> Publishers
        { get; } =
                new(
                    StringComparer.OrdinalIgnoreCase);

        public AuthenticodePublisherIdentity? Read(
            string filePath)
        {
            Publishers.TryGetValue(
                filePath,
                out AuthenticodePublisherIdentity?
                    publisher);

            return publisher;
        }
    }
}
