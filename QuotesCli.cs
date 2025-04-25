using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace QuoteFetcher;

public sealed class QuotesCli : Command<QuotesCli.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<user>")]
        public string User { get; init; } = default!;

        [CommandOption("--out|-o")]
        public string? OutFile { get; init; }
    }

    private readonly QuotesService _svc;
    public QuotesCli(QuotesService svc) => _svc = svc;

    public override int Execute(CommandContext ctx, Settings s)
    {
        var console = AnsiConsole.Console;
        var rows = new List<QuoteDto>();

        try
        {
            console.MarkupLine($"[yellow]Fetching quotes for user: {s.User}[/]");

            var enumerator = _svc.GetAllQuotesAsync(s.User, console, CancellationToken.None).GetAsyncEnumerator();
            while (enumerator.MoveNextAsync().GetAwaiter().GetResult())
            {
                rows.Add(enumerator.Current);
            }

            if (rows.Count == 0)
            {
                console.MarkupLine("[yellow]No quotes found for this user.[/]");
                return 0;
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
                table.AddRow(
                    i++.ToString(),
                    r.Page ?? "-",
                    new Markup(Markup.Escape(r.Text)).ToString()
                );
            }

            console.Write(table);
            console.MarkupLine($"[green]Found {rows.Count} quotes[/]");

            // Save to file if requested
            if (s.OutFile is { } path)
            {
                try
                {
                    console.MarkupLine($"[yellow]Saving to: {path}[/]");
                    File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
                    console.MarkupLine("[green]Save completed successfully[/]");
                }
                catch (Exception ex)
                {
                    console.MarkupLine($"[red]Error saving file: {ex.Message}[/]");
                    return 1;
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            console.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }
    }
}

