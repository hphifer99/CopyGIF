using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace CopyGIF.Platform.Windows.Updates;

internal enum AuthenticodeVerificationStatus
{
    Trusted,
    InvalidSignature,
    UntrustedPublisher
}

internal interface IAuthenticodeVerifier
{
    AuthenticodeVerificationStatus Verify(
        string filePath);
}

internal interface IUpdatePublisherTrustPolicy
{
    bool IsTrustedPublisher(
        string packagePath);
}

internal sealed record AuthenticodePublisherIdentity(
    string Subject,
    string Issuer);

internal interface ISignedFilePublisherReader
{
    AuthenticodePublisherIdentity? Read(
        string filePath);
}

internal sealed class SignedFilePublisherReader :
    ISignedFilePublisherReader
{
    private const uint QueryObjectFile = 1;
    private const uint QueryContentPkcs7Signed =
        0x00000100;
    private const uint QueryContentPkcs7SignedEmbed =
        0x00000400;
    private const uint QueryFormatBinary =
        0x00000002;
    private const uint SignerInformationParameter = 6;
    private const uint CertificateEncoding =
        0x00000001;
    private const uint Pkcs7Encoding =
        0x00010000;
    private const uint FindSubjectCertificate =
        0x000B0000;

    public AuthenticodePublisherIdentity? Read(
        string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        nint certificateStore = nint.Zero;
        nint cryptographicMessage = nint.Zero;
        nint signerInformationPointer = nint.Zero;
        nint certificateContextPointer = nint.Zero;

        try
        {
            bool queried = CryptQueryObject(
                QueryObjectFile,
                filePath,
                QueryContentPkcs7Signed |
                    QueryContentPkcs7SignedEmbed,
                QueryFormatBinary,
                flags: 0,
                out _,
                out _,
                out _,
                out certificateStore,
                out cryptographicMessage,
                nint.Zero);

            if (!queried ||
                certificateStore == nint.Zero ||
                cryptographicMessage == nint.Zero)
            {
                return null;
            }

            uint signerInformationSize = 0;

            if (!CryptMsgGetParam(
                    cryptographicMessage,
                    SignerInformationParameter,
                    index: 0,
                    nint.Zero,
                    ref signerInformationSize) ||
                signerInformationSize == 0 ||
                signerInformationSize > int.MaxValue)
            {
                return null;
            }

            signerInformationPointer =
                Marshal.AllocHGlobal(
                    checked((int)signerInformationSize));

            if (!CryptMsgGetParam(
                    cryptographicMessage,
                    SignerInformationParameter,
                    index: 0,
                    signerInformationPointer,
                    ref signerInformationSize))
            {
                return null;
            }

            CryptographicMessageSignerInformation
                signerInformation =
                    Marshal.PtrToStructure<
                        CryptographicMessageSignerInformation>(
                            signerInformationPointer);

            CertificateInformation certificateInformation =
                new()
                {
                    Issuer = signerInformation.Issuer,
                    SerialNumber =
                        signerInformation.SerialNumber
                };

            certificateContextPointer =
                CertFindCertificateInStore(
                    certificateStore,
                    CertificateEncoding |
                        Pkcs7Encoding,
                    findFlags: 0,
                    FindSubjectCertificate,
                    ref certificateInformation,
                    nint.Zero);

            if (certificateContextPointer ==
                nint.Zero)
            {
                return null;
            }

            CertificateContext certificateContext =
                Marshal.PtrToStructure<
                    CertificateContext>(
                        certificateContextPointer);

            if (certificateContext.EncodedCertificate ==
                    nint.Zero ||
                certificateContext.EncodedSize == 0 ||
                certificateContext.EncodedSize >
                    int.MaxValue)
            {
                return null;
            }

            byte[] encodedCertificate =
                new byte[
                    checked((int)
                        certificateContext.EncodedSize)];

            Marshal.Copy(
                certificateContext.EncodedCertificate,
                encodedCertificate,
                startIndex: 0,
                encodedCertificate.Length);

            using X509Certificate2 certificate =
                X509CertificateLoader.LoadCertificate(
                    encodedCertificate);

            if (string.IsNullOrWhiteSpace(
                    certificate.Subject) ||
                string.IsNullOrWhiteSpace(
                    certificate.Issuer))
            {
                return null;
            }

            return new AuthenticodePublisherIdentity(
                certificate.Subject,
                certificate.Issuer);
        }
        catch (CryptographicException)
        {
            return null;
        }
        finally
        {
            if (certificateContextPointer !=
                nint.Zero)
            {
                _ = CertFreeCertificateContext(
                    certificateContextPointer);
            }

            if (signerInformationPointer !=
                nint.Zero)
            {
                Marshal.FreeHGlobal(
                    signerInformationPointer);
            }

            if (cryptographicMessage !=
                nint.Zero)
            {
                _ = CryptMsgClose(
                    cryptographicMessage);
            }

            if (certificateStore !=
                nint.Zero)
            {
                _ = CertCloseStore(
                    certificateStore,
                    flags: 0);
            }
        }
    }

    [DllImport(
        "crypt32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptQueryObject(
        uint objectType,
        [MarshalAs(UnmanagedType.LPWStr)]
        string objectData,
        uint expectedContentTypeFlags,
        uint expectedFormatTypeFlags,
        uint flags,
        out uint messageAndCertificateEncodingType,
        out uint contentType,
        out uint formatType,
        out nint certificateStore,
        out nint cryptographicMessage,
        nint contextPointer);

    [DllImport(
        "crypt32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptMsgGetParam(
        nint cryptographicMessage,
        uint parameterType,
        uint index,
        nint data,
        ref uint dataSize);

    [DllImport(
        "crypt32.dll",
        SetLastError = true)]
    private static extern nint CertFindCertificateInStore(
        nint certificateStore,
        uint certificateEncodingType,
        uint findFlags,
        uint findType,
        ref CertificateInformation findParameter,
        nint previousCertificateContext);

    [DllImport("crypt32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CertFreeCertificateContext(
        nint certificateContext);

    [DllImport("crypt32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptMsgClose(
        nint cryptographicMessage);

    [DllImport("crypt32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CertCloseStore(
        nint certificateStore,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct CryptographicDataBlob
    {
        public uint DataSize;

        public nint Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CryptographicAlgorithmIdentifier
    {
        public nint ObjectIdentifier;

        public CryptographicDataBlob Parameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CryptographicAttributes
    {
        public uint AttributeCount;

        public nint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CryptographicMessageSignerInformation
    {
        public uint Version;

        public CryptographicDataBlob Issuer;

        public CryptographicDataBlob SerialNumber;

        public CryptographicAlgorithmIdentifier
            HashAlgorithm;

        public CryptographicAlgorithmIdentifier
            HashEncryptionAlgorithm;

        public CryptographicDataBlob EncryptedHash;

        public CryptographicAttributes
            AuthenticatedAttributes;

        public CryptographicAttributes
            UnauthenticatedAttributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CryptographicBitBlob
    {
        public uint DataSize;

        public nint Data;

        public uint UnusedBitCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertificatePublicKeyInformation
    {
        public CryptographicAlgorithmIdentifier
            Algorithm;

        public CryptographicBitBlob PublicKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint LowDateTime;

        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertificateInformation
    {
        public uint Version;

        public CryptographicDataBlob SerialNumber;

        public CryptographicAlgorithmIdentifier
            SignatureAlgorithm;

        public CryptographicDataBlob Issuer;

        public NativeFileTime NotBefore;

        public NativeFileTime NotAfter;

        public CryptographicDataBlob Subject;

        public CertificatePublicKeyInformation
            SubjectPublicKeyInformation;

        public CryptographicBitBlob
            IssuerUniqueIdentifier;

        public CryptographicBitBlob
            SubjectUniqueIdentifier;

        public uint ExtensionCount;

        public nint Extensions;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertificateContext
    {
        public uint CertificateEncodingType;

        public nint EncodedCertificate;

        public uint EncodedSize;

        public nint CertificateInformation;

        public nint CertificateStore;
    }
}

internal sealed class CurrentExecutablePublisherTrustPolicy :
    IUpdatePublisherTrustPolicy
{
    private readonly ISignedFilePublisherReader
        _publisherReader;

    private readonly Func<string?>
        _currentExecutablePath;

    public CurrentExecutablePublisherTrustPolicy()
        : this(
            new SignedFilePublisherReader(),
            () => Environment.ProcessPath)
    {
    }

    internal CurrentExecutablePublisherTrustPolicy(
        ISignedFilePublisherReader publisherReader,
        Func<string?> currentExecutablePath)
    {
        _publisherReader =
            publisherReader ??
            throw new ArgumentNullException(
                nameof(publisherReader));

        _currentExecutablePath =
            currentExecutablePath ??
            throw new ArgumentNullException(
                nameof(currentExecutablePath));
    }

    public bool IsTrustedPublisher(
        string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            packagePath);

        string? executablePath =
            _currentExecutablePath();

        if (string.IsNullOrWhiteSpace(
                executablePath))
        {
            return false;
        }

        AuthenticodePublisherIdentity?
            packagePublisher =
                _publisherReader.Read(
                    packagePath);

        AuthenticodePublisherIdentity?
            applicationPublisher =
                _publisherReader.Read(
                    executablePath);

        return packagePublisher is not null &&
               applicationPublisher is not null &&
               string.Equals(
                   packagePublisher.Subject,
                   applicationPublisher.Subject,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   packagePublisher.Issuer,
                   applicationPublisher.Issuer,
                   StringComparison.OrdinalIgnoreCase);
    }
}
