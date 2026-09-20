using CopyGIF.Core.Contracts;
using System.Windows.Forms;

namespace CopyGIF.Platform.Windows.Shell;

public sealed class FolderPickerService :
    IFolderPickerService
{
    private readonly IWindowHandleProvider
        _windowHandleProvider;

    public FolderPickerService(
        IWindowHandleProvider windowHandleProvider)
    {
        _windowHandleProvider =
            windowHandleProvider ??
            throw new ArgumentNullException(
                nameof(windowHandleProvider));
    }

    public Task<string?> PickFolderAsync(
        string? initialDirectory = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Thread.CurrentThread
                .GetApartmentState() !=
            ApartmentState.STA)
        {
            throw new InvalidOperationException(
                "The folder picker must be opened from the application UI thread.");
        }

        using FolderBrowserDialog dialog =
            new()
            {
                Description =
                    "Choose where CopyGIF stores Favorites and Recents",
                ShowNewFolderButton = true,
                UseDescriptionForTitle = true
            };

        string? normalizedInitialDirectory =
            NormalizeExistingDirectory(
                initialDirectory);

        if (normalizedInitialDirectory is not null)
        {
            dialog.InitialDirectory =
                normalizedInitialDirectory;
        }

        nint ownerWindowHandle =
            _windowHandleProvider
                .GetWindowHandle();

        DialogResult result =
            ownerWindowHandle == nint.Zero
                ? dialog.ShowDialog()
                : dialog.ShowDialog(
                    new NativeWindowOwner(
                        ownerWindowHandle));

        cancellationToken.ThrowIfCancellationRequested();

        if (result != DialogResult.OK ||
            string.IsNullOrWhiteSpace(
                dialog.SelectedPath))
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(
            Path.GetFullPath(
                dialog.SelectedPath));
    }

    internal static string? NormalizeExistingDirectory(
        string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        try
        {
            string fullPath =
                Path.GetFullPath(directory);

            return Directory.Exists(fullPath)
                ? fullPath
                : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (PathTooLongException)
        {
            return null;
        }
    }

    private sealed class NativeWindowOwner :
        IWin32Window
    {
        public NativeWindowOwner(
            nint handle)
        {
            Handle = handle;
        }

        public nint Handle { get; }
    }
}
