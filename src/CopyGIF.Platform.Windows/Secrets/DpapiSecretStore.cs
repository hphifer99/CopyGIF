using System.Security.Cryptography;
using System.Text;
using CopyGIF.Core.Contracts;

namespace CopyGIF.Platform.Windows.Secrets;

public sealed class DpapiSecretStore : ISecretStore
{
    private static readonly byte[] Entropy =
        SHA256.HashData(
            Encoding.UTF8.GetBytes(
                "CopyGIF.SecretStore.v1"));

    private readonly string _secretsDirectory;

    public DpapiSecretStore(string secretsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            secretsDirectory);

        _secretsDirectory =
            Path.GetFullPath(secretsDirectory);
    }

    public async Task<string?> GetAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        string path = GetSecretPath(name);

        if (!File.Exists(path))
        {
            return null;
        }

        byte[] protectedBytes =
            await File.ReadAllBytesAsync(
                path,
                cancellationToken);

        byte[]? plainBytes = null;

        try
        {
            plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException(
                "The protected CopyGIF credential could not be decrypted.",
                exception);
        }
        finally
        {
            if (plainBytes is not null)
            {
                CryptographicOperations.ZeroMemory(
                    plainBytes);
            }
        }
    }

    public async Task SetAsync(
        string name,
        string value,
        CancellationToken cancellationToken = default)
    {
        ValidateName(name);

        ArgumentNullException.ThrowIfNull(value);

        Directory.CreateDirectory(
            _secretsDirectory);

        string path = GetSecretPath(name);
        string temporaryPath = path + ".tmp";

        byte[] plainBytes =
            Encoding.UTF8.GetBytes(value);

        byte[]? protectedBytes = null;

        try
        {
            protectedBytes = ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            await File.WriteAllBytesAsync(
                temporaryPath,
                protectedBytes,
                cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            File.Move(
                temporaryPath,
                path,
                overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                plainBytes);

            if (protectedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(
                    protectedBytes);
            }

            TryDelete(temporaryPath);
        }
    }

    public Task DeleteAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string path = GetSecretPath(name);

        TryDelete(path);

        return Task.CompletedTask;
    }

    private string GetSecretPath(string name)
    {
        ValidateName(name);

        byte[] nameBytes =
            Encoding.UTF8.GetBytes(name.Trim());

        try
        {
            byte[] hash =
                SHA256.HashData(nameBytes);

            string fileName =
                Convert.ToHexString(hash)
                    .ToLowerInvariant() +
                ".bin";

            return Path.Combine(
                _secretsDirectory,
                fileName);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                nameBytes);
        }
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Length > 200)
        {
            throw new ArgumentException(
                "Secret names cannot exceed 200 characters.",
                nameof(name));
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
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