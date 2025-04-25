using System.Text.Json.Serialization;

namespace QuoteFetcher;

public record Quote
{
    [JsonPropertyName("parse")]
    public Parse Parse { get; init; } = null!;
    
    [JsonPropertyName("sayfa_no")]
    public string? PageNo { get; init; }
}