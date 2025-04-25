using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console.Cli;

namespace QuoteFetcher;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var app = new CommandApp<QuotesCli>(new TypeRegistrar(await BuildRegistryAsync()));
            return await app.RunAsync(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<IServiceProvider> BuildRegistryAsync()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(s =>
            {
                s.AddHttpClient("1000kitap", c =>
                {
                    c.BaseAddress = new Uri("https://api.1000kitap.com/");
                    c.DefaultRequestHeaders.Add("User-Agent",
                        "QuotesFetcher/1.0 (+github.com/yourname)");
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5));
                
                s.AddTransient<QuotesService>();
            })
            .Build();

        return host.Services;
    }
}



