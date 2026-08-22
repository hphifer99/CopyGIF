using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CopyGIF.Services
{
    public sealed class PreviewCacheService
    {
        private readonly HttpClient _httpClient;
        private readonly string _cacheDirectory;
        private readonly SemaphoreSlim _downloadSlot =
            new SemaphoreSlim(1, 1);

        public PreviewCacheService(HttpClient httpClient)
        {
            _httpClient = httpClient
                ?? throw new ArgumentNullException(
                    nameof(httpClient));

            string cacheDirectory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder
                        .LocalApplicationData),
                "CopyGIF",
                "Cache",
                "Previews");

            try
            {
                Directory.CreateDirectory(cacheDirectory);
            }
            catch (IOException)
            {
                cacheDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "CopyGIF",
                    "Cache",
                    "Previews");

                Directory.CreateDirectory(cacheDirectory);
            }
            catch (UnauthorizedAccessException)
            {
                cacheDirectory = Path.Combine(
                    Path.GetTempPath(),
                    "CopyGIF",
                    "Cache",
                    "Previews");

                Directory.CreateDirectory(cacheDirectory);
            }

            _cacheDirectory = cacheDirectory;
            RemoveStaleFiles();
        }

        public async Task<string> GetLocalPreviewAsync(
            string previewUrl,
            CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(
                    previewUrl,
                    UriKind.Absolute,
                    out Uri previewUri) ||
                previewUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException(
                    "The preview URL must use HTTPS.",
                    nameof(previewUrl));
            }

            string destinationPath = Path.Combine(
                _cacheDirectory,
                CreateCacheFileName(previewUrl));

            if (IsUsableFile(destinationPath))
            {
                Touch(destinationPath);
                return destinationPath;
            }

            await _downloadSlot
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            string temporaryPath =
                destinationPath + "." +
                Guid.NewGuid().ToString("N") +
                ".tmp";

            try
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                if (IsUsableFile(destinationPath))
                {
                    Touch(destinationPath);
                    return destinationPath;
                }

                using (HttpResponseMessage response =
                    await _httpClient.GetAsync(
                            previewUri,
                            HttpCompletionOption
                                .ResponseHeadersRead,
                            cancellationToken)
                        .ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream source =
                        await response.Content
                            .ReadAsStreamAsync()
                            .ConfigureAwait(false))
                    using (var destination =
                        new FileStream(
                            temporaryPath,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None,
                            81920,
                            true))
                    {
                        await source.CopyToAsync(
                                destination,
                                81920,
                                cancellationToken)
                            .ConfigureAwait(false);

                        await destination
                            .FlushAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                cancellationToken
                    .ThrowIfCancellationRequested();

                ValidateGifFile(temporaryPath);

                if (!File.Exists(destinationPath))
                {
                    File.Move(
                        temporaryPath,
                        destinationPath);
                }

                Touch(destinationPath);
                return destinationPath;
            }
            finally
            {
                TryDelete(temporaryPath);
                _downloadSlot.Release();
            }
        }

        private void RemoveStaleFiles()
        {
            DateTime cutoff =
                DateTime.UtcNow.AddDays(-7);

            try
            {
                foreach (string filePath in
                    Directory.EnumerateFiles(
                        _cacheDirectory,
                        "*.gif",
                        SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(
                                filePath) < cutoff)
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
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void ValidateGifFile(
            string filePath)
        {
            var signature = new byte[6];

            using (var stream =
                File.OpenRead(filePath))
            {
                int bytesRead = stream.Read(
                    signature,
                    0,
                    signature.Length);

                if (bytesRead != signature.Length)
                {
                    throw new InvalidDataException(
                        "The GIF preview download was incomplete.");
                }
            }

            string header =
                Encoding.ASCII.GetString(signature);

            if (!string.Equals(
                    header,
                    "GIF87a",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    header,
                    "GIF89a",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "The preview download was not a GIF.");
            }
        }

        private static string CreateCacheFileName(
            string previewUrl)
        {
            byte[] urlBytes =
                Encoding.UTF8.GetBytes(previewUrl);

            byte[] hash;

            using (SHA256 sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(urlBytes);
            }

            var fileName =
                new StringBuilder(hash.Length * 2 + 4);

            foreach (byte value in hash)
            {
                fileName.Append(
                    value.ToString("x2"));
            }

            fileName.Append(".gif");
            return fileName.ToString();
        }

        private static bool IsUsableFile(
            string filePath)
        {
            try
            {
                return File.Exists(filePath) &&
                       new FileInfo(filePath).Length > 6;
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

        private static void Touch(string filePath)
        {
            try
            {
                File.SetLastWriteTimeUtc(
                    filePath,
                    DateTime.UtcNow);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryDelete(
            string filePath)
        {
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
