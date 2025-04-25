using System.Net.Http.Json;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Spectre.Console;
using System.Threading.Tasks;
using System.Threading;

namespace QuoteFetcher;

public sealed class QuotesService
{
    private readonly IHttpClientFactory _factory;
    
    public QuotesService(IHttpClientFactory factory) => _factory = factory;

    public async IAsyncEnumerable<QuoteDto> GetAllQuotesAsync(
        string user,
        IAnsiConsole console,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var http = _factory.CreateClient("1000kitap");
        int page = 1;
        long cluster = 0;
        int z = 0;
        QuotesResponse? payload = null;

        do
        {
            try
            {
                var url = $"okurCekV2?id={user}&bolum=alintilar" +
                         $"&sayfa={page}&kume={cluster}&z={z}" +
                         "&appVersion=2.43.2&os=web&hl=tr";

                console.MarkupLine($"[grey]Fetching page {page}...[/]");

                payload = await http.GetFromJsonAsync<QuotesResponse>(url, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }, ct);

                if (payload is null)
                {
                    console.MarkupLine("[red]Received null response from API[/]");
                    yield break;
                }

                console.MarkupLine($"[yellow]Page {page} of {payload.TotalPages}[/]");

                foreach (var post in payload.Posts)
                {
                    if (post?.Alt?.Quote is null) continue;

                    QuoteDto? dto = null;
                    try
                    {
                        dto = ToDto(post.Alt.Quote);
                    }
                    catch (Exception ex)
                    {
                        console.MarkupLine($"[red]Error processing quote: {ex.Message}[/]");
                    }

                }

                page++;
                cluster = payload.Cluster;
                z = payload.Z;
            }
            catch (HttpRequestException ex)
            {
                console.MarkupLine($"[red]HTTP error: {ex.Message}[/]");
                yield break;
            }
            catch (JsonException ex)
            {
                console.MarkupLine($"[red]JSON parsing error: {ex.Message}[/]");
                yield break;
            }
            catch (Exception ex)
            {
                console.MarkupLine($"[red]Unexpected error: {ex.Message}[/]");
                yield break;
            }
        }
        while (payload is not null && page <= payload.TotalPages && !ct.IsCancellationRequested);
    }

    private static QuoteDto? ToDto(Quote quote)
    {
        try
        {
            var parts = new List<string>();
            
            if (quote.Parse.Raw.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in quote.Parse.Raw.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        parts.Add(element.GetString() ?? string.Empty);
                    }
                    else if (element.ValueKind == JsonValueKind.Object &&
                             element.TryGetProperty("item", out var item))
                    {
                        parts.Add(item.GetString() ?? string.Empty);
                    }
                }
            }

            var text = string.Join("", parts).Trim();
            return string.IsNullOrEmpty(text) ? null : new QuoteDto(text, quote.PageNo);
        }
        catch (Exception)
        {
            return null;
        }
    }
}