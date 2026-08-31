using System.Text.Json;
using System.Text.Json.Serialization;
using CopyGIF.Core.Contracts;
using CopyGIF.Core.Settings;

namespace CopyGIF.Infrastructure.Storage;

public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly ApplicationPaths _paths;

    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();

    public JsonSettingsStore(ApplicationPaths paths)
    {
        _paths = paths ??
            throw new ArgumentNullException(nameof(paths));
    }

    public async Task<AppSettings> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectoriesExist();

        AppSettings? settings = await TryLoadAsync(
            _paths.SettingsPath,
            cancellationToken);

        if (settings is not null)
        {
            return AppSettingsNormalizer.Normalize(settings);
        }

        AppSettings? backup = await TryLoadAsync(
            _paths.SettingsBackupPath,
            cancellationToken);

        if (backup is not null)
        {
            return AppSettingsNormalizer.Normalize(backup);
        }

        return AppSettingsNormalizer.Normalize(
            new AppSettings());
    }

    public async Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _paths.EnsureDirectoriesExist();

        await EnsureNoLegacySettingsAsync(
            cancellationToken);

        AppSettings normalized =
            AppSettingsNormalizer.Normalize(settings);

        string temporaryPath =
            _paths.SettingsPath + ".tmp";

        try
        {
            await using (
                FileStream stream = new(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    options:
                        FileOptions.Asynchronous |
                        FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    normalized,
                    SerializerOptions,
                    cancellationToken);

                await stream.FlushAsync(
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(_paths.SettingsPath))
            {
                File.Replace(
                    temporaryPath,
                    _paths.SettingsPath,
                    _paths.SettingsBackupPath,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(
                    temporaryPath,
                    _paths.SettingsPath);
            }
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private async Task EnsureNoLegacySettingsAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.SettingsPath))
        {
            return;
        }

        bool isLegacy =
            await IsLegacySettingsFileAsync(
                _paths.SettingsPath,
                cancellationToken);

        if (isLegacy)
        {
            throw new LegacySettingsDetectedException(
                _paths.SettingsPath);
        }
    }

    private static async Task<AppSettings?> TryLoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.Asynchronous);

            using JsonDocument document =
                await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken:
                        cancellationToken);

            if (!document.RootElement.TryGetProperty(
                    "schemaVersion",
                    out _))
            {
                throw new LegacySettingsDetectedException(
                    path);
            }

            return document.RootElement
                .Deserialize<AppSettings>(
                    SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static async Task<bool>
        IsLegacySettingsFileAsync(
            string path,
            CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.Asynchronous);

            using JsonDocument document =
                await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken:
                        cancellationToken);

            return !document.RootElement.TryGetProperty(
                "schemaVersion",
                out _);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static JsonSerializerOptions
        CreateSerializerOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,

            WriteIndented = true
        };

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase));

        return options;
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