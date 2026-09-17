using System.Net.Http;
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

        if (CopyGIF.Core.Policies.ProviderMediaPolicy.IsDirectMediaUri(sourceUri))
            return await TryLoadDirectAsync(bitmap, sourceUri);
        if (!sourceUri.IsAbsoluteUri || !sourceUri.IsFile || sourceUri.IsUnc ||
            !string.IsNullOrEmpty(sourceUri.Host) || !string.IsNullOrEmpty(sourceUri.Query) ||
            !string.IsNullOrEmpty(sourceUri.Fragment)) return false;

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
            CopyGIF.Core.Models.RepairDiagnostics.Record("bitmap-decode", "local", exception.GetType().Name);
            return false;
        }
    }
    private static readonly HttpClient DirectClient = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        MaxConnectionsPerServer = 4,
        ConnectTimeout = TimeSpan.FromSeconds(10)
    }) { Timeout = TimeSpan.FromSeconds(30) };

    private static async Task<bool> TryLoadDirectAsync(BitmapImage bitmap, Uri uri)
    {
        const int maximumBytes = 16 * 1024 * 1024;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true, NoStore = true };
            using var response = await DirectClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > maximumBytes) return false;
            await using var source = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var bytes = new MemoryStream();
            byte[] buffer = new byte[32768];
            int count;
            while ((count = await source.ReadAsync(buffer, deadline.Token)) != 0)
            {
                if (bytes.Length + count > maximumBytes) return false;
                bytes.Write(buffer, 0, count);
            }
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            using (var writer = new Windows.Storage.Streams.DataWriter(stream))
            {
                writer.WriteBytes(bytes.ToArray());
                await writer.StoreAsync();
                writer.DetachStream();
            }
            stream.Seek(0);
            await bitmap.SetSourceAsync(stream);
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or
            OperationCanceledException or COMException or ArgumentException)
        {
            CopyGIF.Core.Models.RepairDiagnostics.Record("direct-bitmap", "giphy", exception.GetType().Name);
            return false;
        }
    }
}
