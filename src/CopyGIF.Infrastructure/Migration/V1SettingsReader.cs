using System.Text.Json;
using CopyGIF.Core.Policies;
using CopyGIF.Core.Settings;

namespace CopyGIF.Infrastructure.Migration;

public sealed class V1SettingsReader
{
    private readonly JsonSerializerOptions
        _serializerOptions;

    public V1SettingsReader()
    {
        _serializerOptions =
            new()
            {
                PropertyNameCaseInsensitive = false,
                MaxDepth = 64
            };
    }

    public async Task<V1SettingsSnapshot?> ReadAsync(
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
            StoragePolicy.MaximumSettingsFileBytes)
        {
            throw new InvalidDataException(
                "The V1 settings file exceeds its maximum allowed size.");
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
                    "The V1 settings root must be a JSON object.");
            }

            if (document.RootElement.TryGetProperty(
                    "schemaVersion",
                    out _))
            {
                throw new InvalidDataException(
                    "The selected settings file is already versioned and is not a V1 settings file.");
            }

            V1SettingsData? data =
                document.RootElement
                    .Deserialize<V1SettingsData>(
                        _serializerOptions);

            if (data is null)
            {
                throw new InvalidDataException(
                    "The V1 settings file did not contain settings.");
            }

            List<string> warnings = [];

            return new V1SettingsSnapshot
            {
                Settings = MapSettings(data),
                Credential =
                    ReadCredential(
                        data,
                        warnings),
                Warnings = warnings
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The V1 settings file contains invalid JSON.",
                exception);
        }
    }

    private static AppSettings MapSettings(
        V1SettingsData data)
    {
        AppSettings defaults =
            new();

        bool hasSavedPlacement =
            data.HasSavedWindowPlacement ?? false;

        AppSettings mapped =
            defaults with
            {
                Hotkey =
                    data.Hotkey ??
                    defaults.Hotkey,

                Search = defaults.Search with
                {
                    ResultsPerSearch =
                        data.ResultsPerSearch ??
                        defaults.Search.ResultsPerSearch,
                    DebounceMilliseconds =
                        data.SearchDebounceMilliseconds ??
                        defaults.Search.DebounceMilliseconds,
                    AnimatePreviews =
                        data.AnimatePreviews ??
                        defaults.Search.AnimatePreviews,
                    AutoLoadMoreResults =
                        data.AutoLoadMoreResults ??
                        defaults.Search.AutoLoadMoreResults
                },

                Library = defaults.Library with
                {
                    RecentLimit =
                        data.RecentLimit ??
                        defaults.Library.RecentLimit,
                    FavoriteLimit =
                        data.FavoriteLimit ??
                        defaults.Library.FavoriteLimit,
                    StoreFavoritesLocally =
                        data.StoreFavoritesLocally ??
                        defaults.Library.StoreFavoritesLocally,
                    StoreRecentsLocally =
                        data.StoreRecentsLocally ??
                        defaults.Library.StoreRecentsLocally
                },

                Window = defaults.Window with
                {
                    PlacementMode =
                        ParsePlacementMode(
                            data.WindowPlacementMode),
                    RememberWindowSize =
                        data.RememberWindowSize ??
                        defaults.Window.RememberWindowSize,
                    Width =
                        data.WindowWidth ??
                        defaults.Window.Width,
                    Height =
                        data.WindowHeight ??
                        defaults.Window.Height,
                    Left = hasSavedPlacement
                        ? data.WindowLeft
                        : null,
                    Top = hasSavedPlacement
                        ? data.WindowTop
                        : null
                },

                Behavior = defaults.Behavior with
                {
                    CloseWhenFocusLost =
                        data.CloseWhenFocusLost ??
                        defaults.Behavior.CloseWhenFocusLost,
                    HideAfterCopy =
                        data.HideAfterCopy ??
                        defaults.Behavior.HideAfterCopy
                },

                Startup = defaults.Startup with
                {
                    StartWithWindows =
                        data.StartWithWindows ??
                        defaults.Startup.StartWithWindows
                }
            };

        return AppSettingsNormalizer.Normalize(mapped);
    }

    private static WindowPlacementMode ParsePlacementMode(
        string? value)
    {
        if (string.Equals(
                value,
                "Remember",
                StringComparison.OrdinalIgnoreCase))
        {
            return WindowPlacementMode.Remember;
        }

        if (string.Equals(
                value,
                "Center",
                StringComparison.OrdinalIgnoreCase))
        {
            return WindowPlacementMode.Center;
        }

        return WindowPlacementMode.Mouse;
    }

    private static V1CredentialPayload? ReadCredential(
        V1SettingsData data,
        List<string> warnings)
    {
        if (!string.IsNullOrWhiteSpace(
                data.ApiKeyProtected))
        {
            string protectedValue =
                data.ApiKeyProtected.Trim();

            if (IsValidBase64(protectedValue))
            {
                return new V1CredentialPayload
                {
                    Kind =
                        V1CredentialKind.DpapiCurrentUser,
                    Value = protectedValue
                };
            }

            warnings.Add(
                "The V1 protected API credential was invalid and was not imported.");
        }

        if (string.IsNullOrWhiteSpace(
                data.ApiKey))
        {
            return null;
        }

        return new V1CredentialPayload
        {
            Kind = V1CredentialKind.Plaintext,
            Value = data.ApiKey.Trim()
        };
    }

    private static bool IsValidBase64(
        string value)
    {
        if (value.Length == 0 ||
            value.Length > 128 * 1024)
        {
            return false;
        }

        byte[] buffer =
            new byte[(value.Length * 3 / 4) + 4];

        return Convert.TryFromBase64String(
            value,
            buffer,
            out _);
    }
}
