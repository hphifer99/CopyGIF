using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;

namespace CopyGIF.App.Services;

internal static class LocalBitmapLoader
{
    public static async Task<bool> TryLoadAsync(BitmapImage bitmap, Uri sourceUri)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(sourceUri);

        if (!sourceUri.IsAbsoluteUri || !sourceUri.IsFile || sourceUri.IsUnc ||
            !string.IsNullOrEmpty(sourceUri.Host) ||
            !string.IsNullOrEmpty(sourceUri.Query) ||
            !string.IsNullOrEmpty(sourceUri.Fragment))
        {
            return false;
        }

        try
        {
            StorageFile imageFile = await StorageFile.GetFileFromPathAsync(sourceUri.LocalPath);
            using var stream = await imageFile.OpenReadAsync();
            await bitmap.SetSourceAsync(stream);
            return true;
        }
        catch (Exception exception) when (exception is IOException or
            UnauthorizedAccessException or COMException or ArgumentException or
            NotSupportedException)
        {
            // A missing, evicted, or undecodable image keeps its placeholder.
            return false;
        }
    }
}
