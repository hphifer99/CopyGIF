using CopyGIF.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CopyGIF.Core.Tests.Models;

[TestClass]
public sealed class CoreModelTests
{
    [TestMethod]
    public void GifIdentity_ConstructorTrimsAndFormatsIdentity()
    {
        GifIdentity identity =
            new(
                " klipy ",
                " gif-123 ");

        Assert.AreEqual(
            "klipy",
            identity.ProviderId);

        Assert.AreEqual(
            "gif-123",
            identity.Id);

        Assert.AreEqual(
            "klipy:gif-123",
            identity.ToString());
    }

    [TestMethod]
    public void GifIdentity_EmptyComponents_AreRejected()
    {
        Assert.ThrowsExactly<
            ArgumentException>(
            () =>
                new GifIdentity(
                    " ",
                    "gif-123"));

        Assert.ThrowsExactly<
            ArgumentException>(
            () =>
                new GifIdentity(
                    "klipy",
                    " "));
    }

    [TestMethod]
    public void GifItem_ExposesStableAndCompatibleIdentities()
    {
        GifItem item = new()
        {
            ProviderId = "klipy",
            Id = "gif-123",

            ThumbnailUri =
                new Uri(
                    "https://example.com/thumbnail.gif"),

            GifUri =
                new Uri(
                    "https://example.com/original.gif")
        };

        Assert.AreEqual(
            new GifIdentity(
                "klipy",
                "gif-123"),
            item.StableIdentity);

        Assert.AreEqual(
            "klipy:gif-123",
            item.Identity);
    }

    [TestMethod]
    public void ProviderDescriptor_SupportsDeclaredCapabilities()
    {
        ProviderDescriptor provider = new()
        {
            Id = "klipy",
            DisplayName = "KLIPY",

            Capabilities =
                ProviderCapabilities.Search |
                ProviderCapabilities.Trending |
                ProviderCapabilities.Pagination
        };

        Assert.IsTrue(
            provider.Supports(
                ProviderCapabilities.Search));

        Assert.IsTrue(
            provider.Supports(
                ProviderCapabilities.Trending));

        Assert.IsFalse(
            provider.Supports(
                ProviderCapabilities.ShareRegistration));

        Assert.IsTrue(
            provider.Supports(
                ProviderCapabilities.Search |
                ProviderCapabilities.Pagination));
    }

    [TestMethod]
    public void GifSearchPage_Empty_HasNoItemsOrContinuation()
    {
        GifSearchPage page =
            GifSearchPage.Empty();

        Assert.AreEqual(
            0,
            page.Items.Count);

        Assert.IsFalse(
            page.HasMore);

        Assert.IsNull(
            page.ContinuationToken);
    }

    [TestMethod]
    public void CredentialValidation_InvalidResultRequiresFailure()
    {
        Assert.ThrowsExactly<
            ArgumentOutOfRangeException>(
            () =>
                CredentialValidationResult.Invalid(
                    "Credential rejected.",
                    CredentialValidationFailure.None));

        CredentialValidationResult result =
            CredentialValidationResult.Invalid(
                "Credential rejected.",
                CredentialValidationFailure
                    .InvalidCredential);

        Assert.IsFalse(
            result.IsValid);

        Assert.AreEqual(
            CredentialValidationFailure
                .InvalidCredential,
            result.Failure);
    }

    [TestMethod]
    public void UpdateVerification_InvalidResultRequiresFailure()
    {
        Assert.ThrowsExactly<
            ArgumentOutOfRangeException>(
            () =>
                UpdatePackageVerificationResult
                    .Invalid(
                        UpdatePackageVerificationFailure
                            .None,
                        "Package rejected."));

        UpdatePackageVerificationResult result =
            UpdatePackageVerificationResult.Invalid(
                UpdatePackageVerificationFailure
                    .HashMismatch,
                "Package hash did not match.");

        Assert.IsFalse(
            result.IsValid);

        Assert.AreEqual(
            UpdatePackageVerificationFailure
                .HashMismatch,
            result.Failure);
    }

    [TestMethod]
    public void MigrationResult_SucceededOnlyForSafeOutcomes()
    {
        MigrationResult notRequired = new()
        {
            Status = MigrationStatus.NotRequired
        };

        MigrationResult completed = new()
        {
            Status = MigrationStatus.Completed
        };

        MigrationResult failed = new()
        {
            Status = MigrationStatus.Failed
        };

        MigrationResult rolledBack = new()
        {
            Status = MigrationStatus.RolledBack
        };

        Assert.IsTrue(
            notRequired.Succeeded);

        Assert.IsTrue(
            completed.Succeeded);

        Assert.IsFalse(
            failed.Succeeded);

        Assert.IsFalse(
            rolledBack.Succeeded);
    }

    [TestMethod]
    public void UpdateDownloadProgress_CalculatesAndCapsPercentage()
    {
        UpdateDownloadProgress half = new()
        {
            BytesReceived = 50,
            TotalBytes = 100
        };

        UpdateDownloadProgress excessive = new()
        {
            BytesReceived = 150,
            TotalBytes = 100
        };

        UpdateDownloadProgress unknown = new()
        {
            BytesReceived = 50,
            TotalBytes = 0
        };

        Assert.AreEqual(
            50d,
            half.Percentage);

        Assert.AreEqual(
            100d,
            excessive.Percentage);

        Assert.AreEqual(
            0d,
            unknown.Percentage);
    }
}
