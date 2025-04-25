using System.Text.Json.Serialization;

namespace QuoteFetcher;

public record Alt
{
    [JsonPropertyName("alinti")]
    public Quote? Quote { get; init; }
}