using System.Text.Json.Serialization;

namespace QuoteFetcher;

public record Post
{
    [JsonPropertyName("alt")]
    public Alt? Alt { get; init; }
}