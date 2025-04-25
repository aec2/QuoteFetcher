using System.Text.Json.Serialization;

namespace QuoteFetcher;

public record QuotesResponse
{
    [JsonPropertyName("posts")]
    public IReadOnlyList<Post> Posts { get; init; } = Array.Empty<Post>();

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; init; }

    [JsonPropertyName("cluster")]
    public long Cluster { get; init; }

    [JsonPropertyName("z")]
    public int Z { get; init; }
}