using System.Net;

namespace CopyGIF.Infrastructure.Klipy;

public static class KlipyHttpClientFactory
{
    public static HttpClient Create()
    {
        SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect = false,

            AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate |
                DecompressionMethods.Brotli,

            ConnectTimeout = TimeSpan.FromSeconds(10)
        };

        return new HttpClient(handler)
        {
            BaseAddress =
                new Uri("https://api.klipy.com/"),

            Timeout =
                TimeSpan.FromSeconds(20)
        };
    }
}