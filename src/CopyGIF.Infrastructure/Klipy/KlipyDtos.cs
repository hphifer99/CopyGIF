using System.Text.Json.Serialization;

namespace CopyGIF.Infrastructure.Klipy;

internal sealed class KlipySearchResponseDto
{
    [JsonPropertyName("results")]
    public List<KlipyResultDto> Results { get; set; } = [];

    [JsonPropertyName("next")]
    public string? Next { get; set; }
}

internal sealed class KlipyResultDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("content_description")]
    public string ContentDescription { get; set; } = string.Empty;

    [JsonPropertyName("media_formats")]
    public Dictionary<string, KlipyMediaDto> MediaFormats { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class KlipyMediaDto
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("dims")]
    public int[] Dimensions { get; set; } = [];

    [JsonPropertyName("size")]
    public long Size { get; set; }
}