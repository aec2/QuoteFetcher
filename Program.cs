using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.CommandLine;
using System.Text.Json;

namespace QuoteFetcher;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            // Build the service provider
            var serviceProvider = BuildServices();

            // Create a direct command-line parser
            var rootCommand = new RootCommand("Fetch quotes from 1000kitap.com");

            // Add the username argument
            var usernameArg = new Argument<string>("username", "The username to fetch quotes for");
            rootCommand.AddArgument(usernameArg);

            // Add the output file option
            var outFileOption = new Option<string?>(
                new[] { "--out", "-o" },
                "The JSON file to save quotes to");
            rootCommand.AddOption(outFileOption);

            // Set the handler
            rootCommand.SetHandler(async (string username, string? outFile) =>
            {
                await RunQuoteFetcher(serviceProvider, username, outFile);
            }, usernameArg, outFileOption);

            // Parse and execute
            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    private static async Task RunQuoteFetcher(IServiceProvider services, string username, string? outFile)
    {
        var console = AnsiConsole.Console;
        var quotesService = services.GetRequiredService<QuotesService>();
        var rows = new List<QuoteDto>();

        try
        {
            console.MarkupLine($"[yellow]Fetching quotes for user: {username}[/]");

            await foreach (var quote in quotesService.GetAllQuotesAsync(username, console, CancellationToken.None))
            {
                rows.Add(quote);
            }

            if (rows.Count == 0)
            {
                console.MarkupLine("[yellow]No quotes found for this user.[/]");
                return;
            }

            // Create and display table
            var table = new Table()
                .Border(TableBorder.Simple)
                .Centered()
                .AddColumn(new TableColumn(" # ").Centered())
                .AddColumn(new TableColumn("Page").Centered())
                .AddColumn(new TableColumn("Quote"));

            int i = 1;
            foreach (var r in rows)
            {
                // Ensure we don't pass null to AddRow by using string literals when needed
                string pageValue = r.Page ?? "-";
                string quoteText = Markup.Escape(r.Text);

                table.AddRow(
                    i++.ToString(),
                    pageValue,
                    quoteText
                );
            }

            console.Write(table);
            console.MarkupLine($"[green]Found {rows.Count} quotes[/]");

            // Save to file if requested
            if (!string.IsNullOrEmpty(outFile))
            {
                try
                {
                    console.MarkupLine($"[yellow]Saving to: {outFile}[/]");
                    await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(rows, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
                    console.MarkupLine("[green]Save completed successfully[/]");
                }
                catch (Exception ex)
                {
                    console.MarkupLine($"[red]Error saving file: {ex.Message}[/]");
                }
            }
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error: {ex.Message}[/]");
        }
    }

    private static IServiceProvider BuildServices()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(s =>
            {
                s.AddHttpClient("1000kitap", c =>
                {
                    c.BaseAddress = new Uri("https://api.1000kitap.com/");

                    // Browser-like headers from your DevTools screenshot
                    c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/123.0.0.0 Safari/537.36");
                    c.DefaultRequestHeaders.Add("Accept", "application/json");
                    c.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                    c.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
                    c.DefaultRequestHeaders.Add("Referer", "https://1000kitap.com/");
                    c.DefaultRequestHeaders.Add("Origin", "https://1000kitap.com");

                    // Security and feature headers
                    c.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Google Chrome\";v=\"123\", \"Not-A-Brand\";v=\"8\", \"Chromium\";v=\"123\"");
                    c.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
                    c.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
                    c.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
                    c.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
                    c.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");

                    // Authorization-related header that appears in the request
                    c.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Content-Type Accept Origin User-Agent DNT Cache-Control X-Ms-ReqToken Keep-Alive X-Requested-With If-Modified-Since");
                })
                .ConfigurePrimaryHttpMessageHandler(() => {
                    var handler = new HttpClientHandler();
                    handler.UseCookies = true;
                    handler.CookieContainer = new System.Net.CookieContainer();

                    // Add the cookie from your browser
                    handler.CookieContainer.Add(new Uri("https://api.1000kitap.com"),
                        new System.Net.Cookie("T-Chaz-Kodu", "kSUFi0w5fJysJXauId"));
                    handler.CookieContainer.Add(new Uri("https://api.1000kitap.com"),
                        new System.Net.Cookie("I-Oturum-Kodu", "eA580TOznLMsCTuETYhNR28Luye8T1MDbu"));

                    return handler;
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));

                s.AddTransient<QuotesService>();
            })
            .Build();

        return host.Services;
    }
}