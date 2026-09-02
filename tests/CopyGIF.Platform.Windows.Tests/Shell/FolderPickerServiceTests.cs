using CopyGIF.Platform.Windows.Shell;

namespace CopyGIF.Platform.Windows.Tests.Shell;

[TestClass]
public sealed class FolderPickerServiceTests
{
    [TestMethod]
    public void NormalizeExistingDirectory_ExistingDirectory_ReturnsFullPath()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "CopyGIF.Tests",
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        try
        {
            string? result =
                FolderPickerService
                    .NormalizeExistingDirectory(
                        directory);

            Assert.AreEqual(
                Path.GetFullPath(directory),
                result);
        }
        finally
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }

    [TestMethod]
    public void NormalizeExistingDirectory_MissingDirectory_ReturnsNull()
    {
        string? result =
            FolderPickerService
                .NormalizeExistingDirectory(
                    Path.Combine(
                        Path.GetTempPath(),
                        Guid.NewGuid()
                            .ToString("N")));

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task PickFolderAsync_CanceledToken_DoesNotOpenDialog()
    {
        FolderPickerService service =
            new(
                new ThrowingWindowHandleProvider());

        using CancellationTokenSource source =
            new();

        source.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () =>
                service.PickFolderAsync(
                    cancellationToken:
                        source.Token));
    }

    private sealed class ThrowingWindowHandleProvider :
        IWindowHandleProvider
    {
        public nint GetWindowHandle()
        {
            throw new AssertFailedException(
                "The canceled picker must not request a window handle.");
        }
    }
}
