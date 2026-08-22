using CopyGIF.Models;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CopyGIF.Services
{
    public sealed class ClipboardService
    {
        private const long MaximumGifBytes = 50L * 1024L * 1024L;

        private static readonly TimeSpan MaximumCacheAge =
            TimeSpan.FromHours(24);

        private readonly HttpClient _httpClient;
        private readonly string _temporaryCacheDirectory;

        private string _lastTemporaryClipboardFile;

        public ClipboardService(HttpClient httpClient)
        {
            _httpClient = httpClient
                ?? throw new ArgumentNullException(nameof(httpClient));

            _temporaryCacheDirectory =
                Path.Combine(Path.GetTempPath(), "CopyGIF");

            Directory.CreateDirectory(_temporaryCacheDirectory);
            CleanupOldTemporaryFiles();
        }

        public async Task<string> DownloadAndCopyAsync(
            GifItem gif,
            CancellationToken cancellationToken)
        {
            if (gif == null)
            {
                throw new ArgumentNullException(nameof(gif));
            }

            string localPath = gif.LocalFilePath;
            bool usesPersistentFile = IsValidGifFile(localPath);

            if (!usesPersistentFile)
            {
                string fileName =
                    BuildSafeStem(gif) + "-" +
                    Guid.NewGuid().ToString("N") + ".gif";

                localPath = await CacheGifAsync(
                    gif,
                    _temporaryCacheDirectory,
                    fileName,
                    true,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            SetClipboardFileWithRetry(localPath);

            string previousTemporaryFile =
                _lastTemporaryClipboardFile;

            _lastTemporaryClipboardFile =
                usesPersistentFile ? null : localPath;

            if (!string.Equals(
                    previousTemporaryFile,
                    _lastTemporaryClipboardFile,
                    StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(previousTemporaryFile);
            }

            return localPath;
        }

        public async Task<string> CacheGifAsync(
            GifItem gif,
            string destinationDirectory,
            string fileName,
            bool overwrite,
            CancellationToken cancellationToken)
        {
            if (gif == null)
            {
                throw new ArgumentNullException(nameof(gif));
            }

            if (string.IsNullOrWhiteSpace(destinationDirectory))
            {
                throw new ArgumentException(
                    "A destination directory is required.",
                    nameof(destinationDirectory));
            }

            if (string.IsNullOrWhiteSpace(fileName) ||
                !string.Equals(
                    Path.GetExtension(fileName),
                    ".gif",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "The cache file name must end in .gif.",
                    nameof(fileName));
            }

            Directory.CreateDirectory(destinationDirectory);

            string finalPath =
                Path.Combine(destinationDirectory, Path.GetFileName(fileName));

            if (!overwrite && IsValidGifFile(finalPath))
            {
                return finalPath;
            }

            if (File.Exists(finalPath) && !IsValidGifFile(finalPath))
            {
                TryDelete(finalPath);
            }

            string partialPath = finalPath + ".download";

            TryDelete(partialPath);

            try
            {
                if (IsValidGifFile(gif.LocalFilePath))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (new FileInfo(gif.LocalFilePath).Length >
                        MaximumGifBytes)
                    {
                        throw new InvalidDataException(
                            "The GIF is larger than the 50 MB safety limit.");
                    }

                    if (string.Equals(
                            Path.GetFullPath(gif.LocalFilePath),
                            Path.GetFullPath(finalPath),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return finalPath;
                    }

                    File.Copy(gif.LocalFilePath, partialPath, true);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                else
                {
                    await DownloadGifAsync(
                        gif.FullGifUrl,
                        partialPath,
                        cancellationToken);
                }

                if (!IsValidGifFile(partialPath))
                {
                    throw new InvalidDataException(
                        "The downloaded file is not a valid GIF.");
                }

                if (File.Exists(finalPath))
                {
                    if (!overwrite && IsValidGifFile(finalPath))
                    {
                        TryDelete(partialPath);
                        return finalPath;
                    }

                    File.Delete(finalPath);
                }

                File.Move(partialPath, finalPath);
                return finalPath;
            }
            catch
            {
                TryDelete(partialPath);
                throw;
            }
        }

        private async Task DownloadGifAsync(
            string url,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri gifUri) ||
                gifUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    "KLIPY returned an invalid GIF URL.");
            }

            using (HttpResponseMessage response =
                await _httpClient.GetAsync(
                    gifUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                long? contentLength =
                    response.Content.Headers.ContentLength;

                if (contentLength.HasValue &&
                    contentLength.Value > MaximumGifBytes)
                {
                    throw new InvalidDataException(
                        "The GIF is larger than the 50 MB safety limit.");
                }

                using (Stream input =
                    await response.Content.ReadAsStreamAsync())
                using (var output = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    true))
                {
                    var buffer = new byte[81920];
                    long totalBytes = 0;

                    while (true)
                    {
                        int bytesRead = await input.ReadAsync(
                            buffer,
                            0,
                            buffer.Length,
                            cancellationToken);

                        if (bytesRead == 0)
                        {
                            break;
                        }

                        totalBytes += bytesRead;

                        if (totalBytes > MaximumGifBytes)
                        {
                            throw new InvalidDataException(
                                "The GIF is larger than the 50 MB safety limit.");
                        }

                        await output.WriteAsync(
                            buffer,
                            0,
                            bytesRead,
                            cancellationToken);
                    }
                }
            }
        }

        private static void SetClipboardFileWithRetry(string filePath)
        {
            var files = new StringCollection { filePath };

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Exception lastException = null;

                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        System.Windows.Forms.Clipboard.SetFileDropList(files);
                        return;
                    }
                    catch (ExternalException exception)
                    {
                        lastException = exception;
                        Thread.Sleep(50);
                    }
                }

                throw new InvalidOperationException(
                    "Windows could not open the clipboard. Try again.",
                    lastException);
            });
        }

        private void CleanupOldTemporaryFiles()
        {
            DateTime cutoffUtc =
                DateTime.UtcNow.Subtract(MaximumCacheAge);

            try
            {
                foreach (string filePath in
                    Directory.EnumerateFiles(_temporaryCacheDirectory))
                {
                    string extension = Path.GetExtension(filePath);

                    bool isCopyGifFile =
                        string.Equals(
                            extension,
                            ".gif",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            extension,
                            ".download",
                            StringComparison.OrdinalIgnoreCase);

                    if (!isCopyGifFile)
                    {
                        continue;
                    }

                    try
                    {
                        if (File.GetLastWriteTimeUtc(filePath) < cutoffUtc)
                        {
                            TryDelete(filePath);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static bool IsValidGifFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) ||
                !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                var header = new byte[6];

                using (var stream = File.Open(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read))
                {
                    if (stream.Read(header, 0, header.Length) != header.Length)
                    {
                        return false;
                    }
                }

                string signature = Encoding.ASCII.GetString(header);

                return signature == "GIF87a" || signature == "GIF89a";
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        public static string BuildSafeStem(GifItem gif)
        {
            if (gif != null && gif.Id != 0)
            {
                return gif.Id.ToString();
            }

            string value = gif?.FullGifUrl ?? Guid.NewGuid().ToString("N");

            using (SHA256 hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(16);

                for (int index = 0; index < 8; index++)
                {
                    builder.Append(bytes[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static void TryDelete(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}

