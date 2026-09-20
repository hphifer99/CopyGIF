namespace CopyGIF.Core.Models;

public sealed record UpdateDownloadProgress
{
    public required long BytesReceived { get; init; }

    public required long TotalBytes { get; init; }

    public double Percentage =>
        TotalBytes > 0
            ? Math.Min(
                100,
                BytesReceived * 100D / TotalBytes)
            : 0;
}

public sealed record DownloadedUpdatePackage
{
    public required UpdateManifest Manifest { get; init; }

    public required string FilePath { get; init; }

    public required long SizeBytes { get; init; }

    public required string Sha256 { get; init; }

    public required DateTimeOffset DownloadedAtUtc { get; init; }
}

public enum UpdatePackageVerificationFailure
{
    None,
    FileMissing,
    SizeMismatch,
    HashMismatch,
    InvalidSignature,
    UntrustedPublisher,
    UnsupportedPackage,

    // The signature looks fine but Windows could not reach (or has no cached answer from) the
    // certificate authority to confirm the certificate was not revoked. This is not a fault of
    // the package, so it is kept and checked again later.
    RevocationCheckUnavailable,
    Unknown
}

public sealed record UpdatePackageVerificationResult
{
    public required bool IsValid { get; init; }

    public UpdatePackageVerificationFailure Failure { get; init; }

    public string? Message { get; init; }

    public static UpdatePackageVerificationResult Valid()
    {
        return new UpdatePackageVerificationResult
        {
            IsValid = true,
            Failure =
                UpdatePackageVerificationFailure.None
        };
    }

    public static UpdatePackageVerificationResult Invalid(
        UpdatePackageVerificationFailure failure,
        string message)
    {
        if (failure ==
            UpdatePackageVerificationFailure.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failure),
                failure,
                "An invalid package requires a failure reason.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            message);

        return new UpdatePackageVerificationResult
        {
            IsValid = false,
            Failure = failure,
            Message = message
        };
    }
}
