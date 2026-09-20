using System.Runtime.InteropServices;

namespace CopyGIF.Platform.Windows.Updates;

internal sealed class WindowsAuthenticodeVerifier :
    IAuthenticodeVerifier
{
    private const uint NoUserInterface = 2;
    private const uint RevokeWholeChain = 1;
    private const uint FileChoice = 1;
    private const uint IgnoreStateAction = 0;
    private const uint RevokeNone = 0;
    private const uint RevocationCheckChainExcludeRoot =
        0x00000080;

    // Documented WINTRUST_DATA provider flags. With these two together and RevokeNone, the
    // check makes no revocation lookup and no network request at all.
    private const uint RevocationCheckNone =
        0x00000010;
    private const uint CacheOnlyUrlRetrieval =
        0x00001000;

    // Results that mean "revocation could not be checked", not "the signature is bad".
    // CRYPT_E_REVOCATION_OFFLINE, CRYPT_E_NO_REVOCATION_CHECK and CERT_E_REVOCATION_FAILURE.
    private const int RevocationOffline =
        unchecked((int)0x80092013);
    private const int NoRevocationCheck =
        unchecked((int)0x80092012);
    private const int RevocationFailure =
        unchecked((int)0x800B010E);

    private static readonly Guid
        GenericVerificationPolicy =
            new(
                "00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private readonly IUpdatePublisherTrustPolicy
        _publisherTrustPolicy;

    public WindowsAuthenticodeVerifier()
        : this(
            new CurrentExecutablePublisherTrustPolicy())
    {
    }

    internal WindowsAuthenticodeVerifier(
        IUpdatePublisherTrustPolicy publisherTrustPolicy)
    {
        _publisherTrustPolicy =
            publisherTrustPolicy ??
            throw new ArgumentNullException(
                nameof(publisherTrustPolicy));
    }

    public AuthenticodeVerificationStatus Verify(
        string filePath) =>
        Verify(
            filePath,
            checkRevocationOnline: true);

    internal static AuthenticodeVerificationStatus
        ClassifyTrustResult(
            int trustResult)
    {
        if (trustResult == 0)
        {
            return AuthenticodeVerificationStatus
                .Trusted;
        }

        return trustResult is
            RevocationOffline or
            NoRevocationCheck or
            RevocationFailure
            ? AuthenticodeVerificationStatus
                .RevocationUnavailable
            : AuthenticodeVerificationStatus
                .InvalidSignature;
    }

    public AuthenticodeVerificationStatus Verify(
        string filePath,
        bool checkRevocationOnline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        nint filePathPointer =
            Marshal.StringToCoTaskMemUni(
                filePath);

        nint fileInfoPointer =
            Marshal.AllocHGlobal(
                Marshal.SizeOf<
                    WinTrustFileInfo>());

        try
        {
            WinTrustFileInfo fileInfo = new()
            {
                StructSize =
                    (uint)Marshal.SizeOf<
                        WinTrustFileInfo>(),
                FilePathPointer =
                    filePathPointer
            };

            Marshal.StructureToPtr(
                fileInfo,
                fileInfoPointer,
                fDeleteOld: false);

            WinTrustData trustData = new()
            {
                StructSize =
                    (uint)Marshal.SizeOf<
                        WinTrustData>(),
                UiChoice = NoUserInterface,
                RevocationChecks =
                    checkRevocationOnline
                        ? RevokeWholeChain
                        : RevokeNone,
                UnionChoice = FileChoice,
                FileInfoPointer = fileInfoPointer,
                StateAction = IgnoreStateAction,
                ProviderFlags =
                    checkRevocationOnline
                        ? RevocationCheckChainExcludeRoot
                        : RevocationCheckNone |
                          CacheOnlyUrlRetrieval
            };

            Guid policy =
                GenericVerificationPolicy;

            int result = WinVerifyTrust(
                nint.Zero,
                ref policy,
                ref trustData);

            AuthenticodeVerificationStatus trustStatus =
                ClassifyTrustResult(
                    result);

            if (trustStatus !=
                AuthenticodeVerificationStatus.Trusted)
            {
                return trustStatus;
            }

            return _publisherTrustPolicy
                .IsTrustedPublisher(
                    filePath)
                ? AuthenticodeVerificationStatus
                    .Trusted
                : AuthenticodeVerificationStatus
                    .UntrustedPublisher;
        }
        finally
        {
            Marshal.FreeHGlobal(
                fileInfoPointer);

            Marshal.FreeCoTaskMem(
                filePathPointer);
        }
    }

    [DllImport(
        "wintrust.dll",
        ExactSpelling = true,
        PreserveSig = true)]
    private static extern int WinVerifyTrust(
        nint windowHandle,
        ref Guid actionIdentifier,
        ref WinTrustData trustData);

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct WinTrustFileInfo
    {
        public uint StructSize;

        public nint FilePathPointer;

        public nint FileHandle;

        public nint KnownSubject;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct WinTrustData
    {
        public uint StructSize;

        public nint PolicyCallbackData;

        public nint SipClientData;

        public uint UiChoice;

        public uint RevocationChecks;

        public uint UnionChoice;

        public nint FileInfoPointer;

        public uint StateAction;

        public nint StateData;

        public nint UrlReference;

        public uint ProviderFlags;

        public uint UiContext;

        public nint SignatureSettings;
    }
}
