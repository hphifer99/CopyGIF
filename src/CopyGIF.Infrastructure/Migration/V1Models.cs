using System.Text.Json;
using System.Text.Json.Serialization;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Infrastructure.Migration;

public enum V1CredentialKind
{
    DpapiCurrentUser,
    Plaintext
}

public sealed record V1CredentialPayload
{
    public required V1CredentialKind Kind { get; init; }

    public required string Value { get; init; }
}

public sealed record V1SettingsSnapshot
{
    public required AppSettings Settings { get; init; }

    public V1CredentialPayload? Credential { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } =
        [];
}

public sealed record V1LibrarySnapshot
{
    public required LibrarySnapshot Library { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } =
        [];
}

internal sealed record V1SettingsData
{
    [JsonPropertyName("ApiKeyProtected")]
    public string? ApiKeyProtected { get; init; }

    [JsonPropertyName("ApiKey")]
    public string? ApiKey { get; init; }

    [JsonPropertyName("Hotkey")]
    public string? Hotkey { get; init; }

    [JsonPropertyName("ResultsPerSearch")]
    public int? ResultsPerSearch { get; init; }

    [JsonPropertyName("SearchDebounceMilliseconds")]
    public int? SearchDebounceMilliseconds { get; init; }

    [JsonPropertyName("RecentLimit")]
    public int? RecentLimit { get; init; }

    [JsonPropertyName("FavoriteLimit")]
    public int? FavoriteLimit { get; init; }

    [JsonPropertyName("WindowPlacementMode")]
    public string? WindowPlacementMode { get; init; }

    [JsonPropertyName("CloseWhenFocusLost")]
    public bool? CloseWhenFocusLost { get; init; }

    [JsonPropertyName("HideAfterCopy")]
    public bool? HideAfterCopy { get; init; }

    [JsonPropertyName("StoreFavoritesLocally")]
    public bool? StoreFavoritesLocally { get; init; }

    [JsonPropertyName("StoreRecentsLocally")]
    public bool? StoreRecentsLocally { get; init; }

    [JsonPropertyName("RememberWindowSize")]
    public bool? RememberWindowSize { get; init; }

    [JsonPropertyName("AnimatePreviews")]
    public bool? AnimatePreviews { get; init; }

    [JsonPropertyName("StartWithWindows")]
    public bool? StartWithWindows { get; init; }

    [JsonPropertyName("AutoLoadMoreResults")]
    public bool? AutoLoadMoreResults { get; init; }

    [JsonPropertyName("WindowLeft")]
    public double? WindowLeft { get; init; }

    [JsonPropertyName("WindowTop")]
    public double? WindowTop { get; init; }

    [JsonPropertyName("WindowWidth")]
    public double? WindowWidth { get; init; }

    [JsonPropertyName("WindowHeight")]
    public double? WindowHeight { get; init; }

    [JsonPropertyName("HasSavedWindowPlacement")]
    public bool? HasSavedWindowPlacement { get; init; }
}

internal sealed record V1LibraryData
{
    [JsonPropertyName("Favorites")]
    public IReadOnlyList<V1GifItemData>? Favorites { get; init; }

    [JsonPropertyName("Recents")]
    public IReadOnlyList<V1GifItemData>? Recents { get; init; }
}

internal sealed record V1GifItemData
{
    [JsonPropertyName("Id")]
    public long Id { get; init; }

    [JsonPropertyName("Title")]
    public string? Title { get; init; }

    [JsonPropertyName("ThumbnailUrl")]
    public string? ThumbnailUrl { get; init; }

    [JsonPropertyName("FullGifUrl")]
    public string? FullGifUrl { get; init; }

    [JsonPropertyName("PreviewGifUrl")]
    public string? PreviewGifUrl { get; init; }

    [JsonPropertyName("Width")]
    public int Width { get; init; }

    [JsonPropertyName("Height")]
    public int Height { get; init; }

    [JsonPropertyName("LocalFilePath")]
    public string? LocalFilePath { get; init; }

    [JsonPropertyName("AddedUtc")]
    public JsonElement AddedUtc { get; init; }
}
