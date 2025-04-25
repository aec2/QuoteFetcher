using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuoteFetcher;

public record Parse
{
    [JsonPropertyName("raw")]
    public JsonElement Raw { get; init; }
}