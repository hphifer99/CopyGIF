using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CopyGIF.Core.Models;
using CopyGIF.Core.Policies;
using CopyGIF.Core.Settings;

namespace CopyGIF.Infrastructure.Migration;

public sealed class V1LibraryReader
{
    private readonly JsonSerializerOptions
        _serializerOptions;

    public V1LibraryReader()
    {
        _serializerOptions =
            new()
            {
                PropertyNameCaseInsensitive = false,
                MaxDepth = 64
            };
    }

    public async Task<V1LibrarySnapshot?> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath =
            Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            return null;
        }

        FileInfo fileInfo =
            new(fullPath);

        if (fileInfo.Length >
            StoragePolicy.MaximumLibraryFileBytes)
        {
            throw new InvalidDataException(
                "The V1 library file exceeds its maximum allowed size.");
        }

        try
        {
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
                throw new InvalidDataException(
                    "The V1 library root must be a JSON object.");
            }

            if (document.RootElement.TryGetProperty(
                    "schemaVersion",
                    out _))
            {
                throw new InvalidDataException(
                    "The selected library file is already versioned and is not a V1 library file.");
            }

            V1LibraryData? data =
                document.RootElement
                    .Deserialize<V1LibraryData>(
                        _serializerOptions);

            if (data is null)
            {
                throw new InvalidDataException(
                    "The V1 library did not contain data.");
            }

            List<string> warnings = [];

            List<LibraryEntry> favorites =
                MapEntries(
                    data.Favorites,
                    isRecent: false,
                    "favorite",
                    warnings);

            List<LibraryEntry> recents =
                MapEntries(
                    data.Recents,
                    isRecent: true,
                    "recent",
                    warnings);

            return new V1LibrarySnapshot
            {
                Library = new LibrarySnapshot
                {
                    Favorites = favorites,
                    Recents = recents
                },
                Warnings = warnings
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The V1 library file contains invalid JSON.",
                exception);
        }
    }

    private static List<LibraryEntry> MapEntries(
        IReadOnlyList<V1GifItemData>? source,
        bool isRecent,
        string description,
        List<string> warnings)
    {
        if (source is null)
        {
            return [];
        }

        List<LibraryEntry> entries = [];

        for (int index = 0;
             index < source.Count;
             index++)
        {
            V1GifItemData item =
                source[index];

            if (TryMapEntry(
                    item,
                    isRecent,
                    out LibraryEntry? entry))
            {
                entries.Add(entry);
                continue;
            }

            warnings.Add(
                $"Skipped V1 {description} item {index + 1} because its metadata was invalid.");
        }

        return entries;
    }

    private static bool TryMapEntry(
        V1GifItemData item,
        bool isRecent,
        [NotNullWhen(true)] out LibraryEntry? entry)
    {
        entry = null;

        if (!TryGetHttpsUri(
                item.FullGifUrl,
                out Uri? gifUri) ||
            !TryGetAddedAtUtc(
                item.AddedUtc,
                out DateTimeOffset addedAtUtc))
        {
            return false;
        }

        Uri thumbnailUri =
            TryGetHttpsUri(
                item.ThumbnailUrl,
                out Uri? thumbnail)
                ? thumbnail
                : gifUri;

        Uri? previewUri =
            TryGetHttpsUri(
                item.PreviewGifUrl,
                out Uri? preview)
                ? preview
                : null;

        entry = new LibraryEntry
        {
            Identity =
                new GifIdentity(
                    AppSettings.DefaultProviderId,
                    GetIdentity(item)),
            Title =
                item.Title?.Trim() ??
                string.Empty,
            GifUri = gifUri,
            ThumbnailUri = thumbnailUri,
            PreviewUri = previewUri,
            LocalFilePath =
                NormalizeLocalPath(
                    item.LocalFilePath),
            Width = Math.Max(0, item.Width),
            Height = Math.Max(0, item.Height),
            AddedAtUtc = addedAtUtc,
            LastCopiedAtUtc = isRecent
                ? addedAtUtc
                : null,
            CopyCount = isRecent ? 1 : 0
        };

        return true;
    }

    private static string GetIdentity(
        V1GifItemData item)
    {
        if (item.Id != 0)
        {
            return item.Id.ToString(
                CultureInfo.InvariantCulture);
        }

        byte[] urlBytes =
            Encoding.UTF8.GetBytes(
                item.FullGifUrl ??
                string.Empty);

        try
        {
            return "url-" +
                   Convert.ToHexString(
                           SHA256.HashData(urlBytes))
                       .ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(
                urlBytes);
        }
    }

    private static bool TryGetHttpsUri(
        string? value,
        [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;

        if (!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out Uri? candidate) ||
            !string.Equals(
                candidate.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = candidate;
        return true;
    }

    private static string? NormalizeLocalPath(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return Path.IsPathFullyQualified(value)
                ? Path.GetFullPath(value)
                : null;
        }
        catch (Exception exception)
            when (exception is
                ArgumentException or
                NotSupportedException or
                PathTooLongException)
        {
            return null;
        }
    }

    private static bool TryGetAddedAtUtc(
        JsonElement element,
        out DateTimeOffset value)
    {
        value = default;

        if (element.ValueKind ==
                JsonValueKind.String &&
            TryParseDate(
                element.GetString(),
                out value))
        {
            return true;
        }

        if (element.ValueKind ==
                JsonValueKind.Number &&
            element.TryGetInt64(
                out long milliseconds))
        {
            try
            {
                value =
                    DateTimeOffset
                        .FromUnixTimeMilliseconds(
                            milliseconds);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryParseDate(
        string? text,
        out DateTimeOffset value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out value))
        {
            return true;
        }

        const string prefix = "/Date(";
        const string suffix = ")/";

        if (!text.StartsWith(
                prefix,
                StringComparison.Ordinal) ||
            !text.EndsWith(
                suffix,
                StringComparison.Ordinal))
        {
            return false;
        }

        string payload =
            text[prefix.Length..^suffix.Length];

        int plusIndex =
            payload.IndexOf(
                '+',
                1);

        int minusIndex =
            payload.IndexOf(
                '-',
                1);

        int offsetIndex =
            plusIndex < 0
                ? minusIndex
                : minusIndex < 0
                    ? plusIndex
                    : Math.Min(
                        plusIndex,
                        minusIndex);

        string millisecondsText =
            offsetIndex > 0
                ? payload[..offsetIndex]
                : payload;

        if (!long.TryParse(
                millisecondsText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long milliseconds))
        {
            return false;
        }

        try
        {
            value =
                DateTimeOffset
                    .FromUnixTimeMilliseconds(
                        milliseconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
