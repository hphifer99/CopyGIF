using System.Text.Json.Serialization;

namespace CopyGIF.Infrastructure.Klipy;

internal sealed class KlipyResponseDto
{
    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("data")]
    public KlipyPageDto? Data { get; set; }
}

internal sealed class KlipyPageDto
{
    [JsonPropertyName("data")]
    public List<KlipyItemDto?>? Items { get; set; } = [];

    [JsonPropertyName("current_page")]
    public int CurrentPage { get; set; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    [JsonPropertyName("has_next")]
    public bool HasNext { get; set; }
}

internal sealed class KlipyItemDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("file")]
    public KlipyFilesDto? Files { get; set; }
}

internal sealed class KlipyFilesDto
{
    [JsonPropertyName("hd")]
    public KlipyFormatsDto? Hd { get; set; }

    [JsonPropertyName("md")]
    public KlipyFormatsDto? Md { get; set; }

    [JsonPropertyName("sm")]
    public KlipyFormatsDto? Sm { get; set; }

    [JsonPropertyName("xs")]
    public KlipyFormatsDto? Xs { get; set; }
}

internal sealed class KlipyFormatsDto
{
    [JsonPropertyName("gif")]
    public KlipyMediaDto? Gif { get; set; }

    [JsonPropertyName("jpg")]
    public KlipyMediaDto? Jpg { get; set; }
}

internal sealed class KlipyMediaDto
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
