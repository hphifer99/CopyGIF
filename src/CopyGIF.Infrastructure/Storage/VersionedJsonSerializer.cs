using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using CopyGIF.Core.Models;

namespace CopyGIF.Infrastructure.Storage;

public sealed class VersionedJsonSerializer
{
    private readonly AtomicFileWriter _fileWriter;
    private readonly CorruptFileRecovery _corruptFileRecovery;
    // Store instances created by tests or other callers still serialize access to the same file.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathGates =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private static readonly JsonSerializerOptions
        SerializerOptions =
            CreateSerializerOptions();

    public VersionedJsonSerializer(
        AtomicFileWriter fileWriter,
        CorruptFileRecovery corruptFileRecovery)
    {
        _fileWriter =
            fileWriter ??
            throw new ArgumentNullException(
                nameof(fileWriter));

        _corruptFileRecovery =
            corruptFileRecovery ??
            throw new ArgumentNullException(
                nameof(corruptFileRecovery));
    }

    internal async Task<T> LoadAsync<T>(
        VersionedJsonStoreDefinition<T> definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        SemaphoreSlim gate = PathGates.GetOrAdd(Path.GetFullPath(definition.PrimaryPath),
            static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await LoadCoreAsync(definition, cancellationToken).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    private async Task<T> LoadCoreAsync<T>(
        VersionedJsonStoreDefinition<T> definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        JsonReadResult<T> primary =
            await ReadAsync(
                definition.PrimaryPath,
                definition,
                cancellationToken);

        if (IsValidSuccess(
                primary,
                definition))
        {
            if (primary.Status == JsonReadStatus.PartiallyRecovered)
            {
                _corruptFileRecovery.Preserve(definition.PrimaryPath);
                await WriteCoreAsync(definition, primary.Value!, cancellationToken);
            }
            return primary.Value!;
        }

        ThrowIfNotRecoverable(
            primary,
            definition,
            definition.PrimaryPath);

        bool primaryWasCorrupt =
            IsCorrupt(
                primary,
                definition);

        if (primaryWasCorrupt)
        {
            _corruptFileRecovery.Preserve(
                definition.PrimaryPath);
        }

        JsonReadResult<T> backup =
            await ReadAsync(
                definition.BackupPath,
                definition,
                cancellationToken);

        if (IsValidSuccess(
                backup,
                definition))
        {
            if (backup.Status == JsonReadStatus.PartiallyRecovered)
                _corruptFileRecovery.Preserve(definition.BackupPath);
            await WriteCoreAsync(
                definition,
                backup.Value!,
                cancellationToken);

            return backup.Value!;
        }

        ThrowIfNotRecoverable(
            backup,
            definition,
            definition.BackupPath);

        bool backupWasCorrupt =
            IsCorrupt(
                backup,
                definition);

        if (backupWasCorrupt)
        {
            _corruptFileRecovery.Preserve(
                definition.BackupPath);
        }

        T defaults =
            definition.CreateDefaults();

        EnsureValid(
            definition,
            defaults);

        if (primaryWasCorrupt ||
            backupWasCorrupt)
        {
            await WriteCoreAsync(
                definition,
                defaults,
                cancellationToken);
        }

        return defaults;
    }

    internal async Task SaveAsync<T>(
        VersionedJsonStoreDefinition<T> definition,
        T value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        SemaphoreSlim gate = PathGates.GetOrAdd(Path.GetFullPath(definition.PrimaryPath),
            static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await SaveCoreAsync(definition, value, cancellationToken).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    private async Task SaveCoreAsync<T>(
        VersionedJsonStoreDefinition<T> definition,
        T value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        ArgumentNullException.ThrowIfNull(
            value);

        EnsureValid(
            definition,
            value);

        JsonReadResult<T> existing =
            await ReadAsync(
                definition.PrimaryPath,
                definition,
                cancellationToken);

        ThrowIfNotRecoverable(
            existing,
            definition,
            definition.PrimaryPath);

        if (IsCorrupt(
                existing,
                definition))
        {
            _corruptFileRecovery.Preserve(
                definition.PrimaryPath);
        }

        await WriteCoreAsync(
            definition,
            value,
            cancellationToken);
    }

    private static async Task<JsonReadResult<T>>
        ReadAsync<T>(
            string path,
            VersionedJsonStoreDefinition<T> definition,
            CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return await ReadCoreAsync(path, definition, cancellationToken).ConfigureAwait(false); }
            catch (IOException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static async Task<JsonReadResult<T>> ReadCoreAsync<T>(
        string path, VersionedJsonStoreDefinition<T> definition, CancellationToken cancellationToken)
    {
        string fullPath =
            Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            return JsonReadResult<T>.Missing();
        }

        try
        {
            FileInfo fileInfo =
                new(fullPath);

            if (fileInfo.Length >
                definition.MaximumBytes)
            {
                return JsonReadResult<T>.Invalid();
            }

            await using FileStream stream =
                new(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 16 * 1024,
                    options:
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan);

            using JsonDocument document =
                await JsonDocument.ParseAsync(
                    stream,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling =
                            JsonCommentHandling.Disallow,
                        MaxDepth = 64
                    },
                    cancellationToken);

            if (document.RootElement.ValueKind !=
                JsonValueKind.Object)
            {
                return JsonReadResult<T>.Invalid();
            }

            if (!document.RootElement.TryGetProperty(
                    "schemaVersion",
                    out JsonElement versionElement))
            {
                return JsonReadResult<T>
                    .MissingSchemaVersion();
            }

            if (versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetInt32(out int schemaVersion))
            {
                return JsonReadResult<T>.Invalid();
            }

            if (schemaVersion !=
                definition.CurrentSchemaVersion)
            {
                return JsonReadResult<T>
                    .Unsupported(
                        schemaVersion);
            }

            T? value = default;
            try { value = document.RootElement.Deserialize<T>(SerializerOptions); }
            catch (JsonException) when (definition.RecoverEntries is not null) { }

            if (value is not null && definition.IsValid(value))
                return JsonReadResult<T>.Success(value);

            if (definition.RecoverEntries is not null)
            {
                T? recovered = definition.RecoverEntries(document.RootElement, SerializerOptions);
                if (recovered is not null && definition.IsValid(recovered))
                    return JsonReadResult<T>.PartiallyRecovered(recovered);
            }

            return JsonReadResult<T>.Invalid();
        }
        catch (JsonException)
        {
            return JsonReadResult<T>.Invalid();
        }
    }

    private async Task WriteCoreAsync<T>(
        VersionedJsonStoreDefinition<T> definition,
        T value,
        CancellationToken cancellationToken)
    {
        byte[] json =
            JsonSerializer.SerializeToUtf8Bytes(
                value,
                SerializerOptions);

        if (json.LongLength >
            definition.MaximumBytes)
        {
            throw new InvalidDataException(
                $"CopyGIF {definition.Description} exceeds its maximum size.");
        }

        await _fileWriter.WriteAsync(
            definition.PrimaryPath,
            definition.BackupPath,
            async (stream, token) =>
                await stream.WriteAsync(
                    json,
                    token),
            cancellationToken);
    }

    private static bool IsValidSuccess<T>(
        JsonReadResult<T> result,
        VersionedJsonStoreDefinition<T> definition)
    {
        return (result.Status is JsonReadStatus.Success or JsonReadStatus.PartiallyRecovered) &&
               result.Value is not null &&
               definition.IsValid(
                   result.Value);
    }

    private static bool IsCorrupt<T>(
        JsonReadResult<T> result,
        VersionedJsonStoreDefinition<T> definition)
    {
        return result.Status is
                   JsonReadStatus.Invalid or
                   JsonReadStatus.MissingSchemaVersion ||
               result.Status == JsonReadStatus.PartiallyRecovered ||
               result.Status ==
                   JsonReadStatus.Success &&
               (result.Value is null ||
                !definition.IsValid(
                    result.Value));
    }

    private static void ThrowIfNotRecoverable<T>(
        JsonReadResult<T> result,
        VersionedJsonStoreDefinition<T> definition,
        string path)
    {
        if (result.Status ==
            JsonReadStatus.UnsupportedSchemaVersion)
        {
            throw new InvalidDataException(
                $"CopyGIF {definition.Description} schema version " +
                $"{result.SchemaVersion} is not supported.");
        }

        if (result.Status ==
                JsonReadStatus.MissingSchemaVersion &&
            definition.MissingSchemaExceptionFactory
                is not null)
        {
            throw definition
                .MissingSchemaExceptionFactory(path);
        }
    }

    private static void EnsureValid<T>(
        VersionedJsonStoreDefinition<T> definition,
        T value)
    {
        if (!definition.IsValid(value))
        {
            throw new InvalidDataException(
                $"CopyGIF {definition.Description} is invalid.");
        }
    }

    private static JsonSerializerOptions
        CreateSerializerOptions()
    {
        JsonSerializerOptions options =
            new()
            {
                MaxDepth = 64,
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling =
                    JsonUnmappedMemberHandling.Disallow,
                WriteIndented = true
            };

        options.Converters.Add(
            new GifIdentityJsonConverter());

        options.Converters.Add(
            new JsonStringEnumConverter(
                JsonNamingPolicy.CamelCase));

        return options;
    }
}

internal sealed class GifIdentityJsonConverter :
    JsonConverter<GifIdentity>
{
    public override GifIdentity Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType !=
            JsonTokenType.StartObject)
        {
            throw new JsonException(
                "A GIF identity must be a JSON object.");
        }

        string? providerId = null;
        string? id = null;

        while (reader.Read())
        {
            if (reader.TokenType ==
                JsonTokenType.EndObject)
            {
                if (string.IsNullOrWhiteSpace(
                        providerId) ||
                    string.IsNullOrWhiteSpace(id))
                {
                    throw new JsonException(
                        "A GIF identity requires providerId and id.");
                }

                return new GifIdentity(
                    providerId,
                    id);
            }

            if (reader.TokenType !=
                JsonTokenType.PropertyName)
            {
                throw new JsonException(
                    "A GIF identity contains invalid JSON.");
            }

            string propertyName =
                reader.GetString() ??
                throw new JsonException(
                    "A GIF identity property name is invalid.");

            if (!reader.Read() ||
                reader.TokenType !=
                    JsonTokenType.String)
            {
                throw new JsonException(
                    $"GIF identity property '{propertyName}' must be a string.");
            }

            switch (propertyName)
            {
                case "providerId":
                    if (providerId is not null)
                    {
                        throw new JsonException(
                            "A GIF identity contains duplicate providerId properties.");
                    }

                    providerId =
                        reader.GetString();
                    break;

                case "id":
                    if (id is not null)
                    {
                        throw new JsonException(
                            "A GIF identity contains duplicate id properties.");
                    }

                    id =
                        reader.GetString();
                    break;

                default:
                    throw new JsonException(
                        $"A GIF identity contains unsupported property '{propertyName}'.");
            }
        }

        throw new JsonException(
            "A GIF identity JSON object was not completed.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        GifIdentity value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString(
            "providerId",
            value.ProviderId);

        writer.WriteString(
            "id",
            value.Id);

        writer.WriteEndObject();
    }
}

internal sealed record VersionedJsonStoreDefinition<T>
{
    public required string PrimaryPath { get; init; }

    public required string BackupPath { get; init; }

    public required string Description { get; init; }

    public required long MaximumBytes { get; init; }

    public required int CurrentSchemaVersion { get; init; }

    public required Func<T> CreateDefaults { get; init; }

    public required Func<T, bool> IsValid { get; init; }

    public Func<JsonElement, JsonSerializerOptions, T?>? RecoverEntries { get; init; }

    public Func<string, Exception>?
        MissingSchemaExceptionFactory
    { get; init; }
}

internal enum JsonReadStatus
{
    Success,
    PartiallyRecovered,
    Missing,
    MissingSchemaVersion,
    UnsupportedSchemaVersion,
    Invalid
}

internal sealed record JsonReadResult<T>
{
    private JsonReadResult()
    {
    }

    public required JsonReadStatus Status { get; init; }

    public T? Value { get; init; }

    public int? SchemaVersion { get; init; }

    public static JsonReadResult<T> Success(
        T value)
    {
        return new JsonReadResult<T>
        {
            Status = JsonReadStatus.Success,
            Value = value
        };
    }

    public static JsonReadResult<T> PartiallyRecovered(T value) => new()
    {
        Status = JsonReadStatus.PartiallyRecovered,
        Value = value
    };

    public static JsonReadResult<T> Missing()
    {
        return new JsonReadResult<T>
        {
            Status = JsonReadStatus.Missing
        };
    }

    public static JsonReadResult<T>
        MissingSchemaVersion()
    {
        return new JsonReadResult<T>
        {
            Status =
                JsonReadStatus.MissingSchemaVersion
        };
    }

    public static JsonReadResult<T> Unsupported(
        int schemaVersion)
    {
        return new JsonReadResult<T>
        {
            Status =
                JsonReadStatus.UnsupportedSchemaVersion,
            SchemaVersion = schemaVersion
        };
    }

    public static JsonReadResult<T> Invalid()
    {
        return new JsonReadResult<T>
        {
            Status = JsonReadStatus.Invalid
        };
    }
}
