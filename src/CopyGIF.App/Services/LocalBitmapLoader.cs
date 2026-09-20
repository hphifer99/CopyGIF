using System.Net.Http;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;

namespace CopyGIF.App.Services;

internal static class LocalBitmapLoader
{
    private static readonly SemaphoreSlim DecodeGate = new(4, 4);

    public static async Task<bool> TryLoadAsync(BitmapImage bitmap, Uri sourceUri, CancellationToken token = default)
    {
        bool entered = false;
        try
        {
            await DecodeGate.WaitAsync(token);
            entered = true;
            return await LoadCoreAsync(bitmap, sourceUri, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
        finally { if (entered) DecodeGate.Release(); }
    }

    private static async Task<bool> LoadCoreAsync(BitmapImage bitmap, Uri sourceUri, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(sourceUri);

        if (CopyGIF.Core.Policies.ProviderMediaPolicy.IsDirectMediaUri(sourceUri))
            return await TryLoadDirectAsync(bitmap, sourceUri, token);
        if (!sourceUri.IsAbsoluteUri || !sourceUri.IsFile || sourceUri.IsUnc ||
            !string.IsNullOrEmpty(sourceUri.Host) || !string.IsNullOrEmpty(sourceUri.Query) ||
            !string.IsNullOrEmpty(sourceUri.Fragment)) return false;

        try
        {
            StorageFile imageFile = await StorageFile.GetFileFromPathAsync(sourceUri.LocalPath).AsTask(token);
            using var stream = await imageFile.OpenReadAsync().AsTask(token);
            await bitmap.SetSourceAsync(stream).AsTask(token);
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

    private static async Task<bool> TryLoadDirectAsync(BitmapImage bitmap, Uri uri, CancellationToken token)
    {
        const int maximumBytes = 16 * 1024 * 1024;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true, NoStore = true };
            using var response = await DirectClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > maximumBytes) return false;
            await using var source = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            using (Stream output = stream.AsStreamForWrite())
            {
                byte[] buffer = new byte[32768];
                long total = 0;
                int count;
                while ((count = await source.ReadAsync(buffer, deadline.Token)) != 0)
                {
                    total += count;
                    if (total > maximumBytes) return false;
                    await output.WriteAsync(buffer.AsMemory(0, count), deadline.Token);
                }
                await output.FlushAsync(deadline.Token);
                // Decode while the owning stream is still alive.
                stream.Seek(0);
                await bitmap.SetSourceAsync(stream).AsTask(deadline.Token);
                return true;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or
            OperationCanceledException or COMException or ArgumentException)
        {
            CopyGIF.Core.Models.RepairDiagnostics.Record("direct-bitmap", "local", exception.GetType().Name);
            return false;
        }
    }
}
