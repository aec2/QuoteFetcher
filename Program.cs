using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Threading;

class Program
{
    record QuoteRecord(string Quote, string Book, string Author);

    static void Main(string[] args)
    {
        Console.WriteLine("Starting Selenium scraping...");

        var chromeService = ChromeDriverService.CreateDefaultService();
        chromeService.HideCommandPromptWindow = true;
        chromeService.SuppressInitialDiagnosticInformation = true;

        var chromeOptions = new ChromeOptions();

        // Use a temporary clean user-data-dir to avoid profile conflicts
        string tempProfileDir = Path.Combine(Path.GetTempPath(), "selenium-chrome-profile");
        Console.WriteLine($"Using temporary profile directory: {tempProfileDir}");
        Directory.CreateDirectory(tempProfileDir);

        chromeOptions.AddArgument($"--user-data-dir={tempProfileDir}");
        chromeOptions.AddArgument("--remote-debugging-port=9222");
        chromeOptions.AddArgument("--disable-extensions");
        chromeOptions.AddArgument("--disable-gpu");
        chromeOptions.AddArgument("--no-sandbox");
        chromeOptions.AddArgument("--disable-dev-shm-usage");
        chromeOptions.AddArgument("--disable-blink-features=AutomationControlled");
        chromeOptions.AddExcludedArgument("enable-automation");
        chromeOptions.AddAdditionalOption("useAutomationExtension", false);

        Console.WriteLine("Launching ChromeDriver...");
        using var driver = new ChromeDriver(chromeService, chromeOptions);

        string baseUrl = "https://1000kitap.com/nakkalAmca/alintilar";
        var allQuotes = new List<QuoteRecord>();
        int currentPage = 1;

        while (true)
        {
            string url = baseUrl + (currentPage == 1 ? "" : $"?sayfa={currentPage}");
            Console.WriteLine($"Navigating to page {currentPage}: {url}");

            try
            {
                driver.Navigate().GoToUrl(url);
                Console.WriteLine("Page loaded successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error navigating to page {currentPage}: {ex.Message}");
                break;
            }

            Thread.Sleep(3000);

            Console.WriteLine("Finding quote elements...");
            var quoteElements = driver.FindElements(By.CssSelector("span.text-alt"));
            if (quoteElements == null || quoteElements.Count == 0)
            {
                Console.WriteLine("No more quotes found. Exiting loop.");
                break;
            }

            Console.WriteLine($"Found {quoteElements.Count} quotes on page {currentPage}.");
            foreach (var element in quoteElements)
            {
                var quote = element.Text.Trim();
                allQuotes.Add(new QuoteRecord(quote, "", ""));
                Console.WriteLine($"Quote {allQuotes.Count}: {quote}");
            }

            currentPage++;
        }

        Console.WriteLine("Writing all quotes to CSV file...");
        string dateSuffix = DateTime.Now.ToString("yyyy-MM-dd");
        string outputPath = $"quotes_from_selenium_{dateSuffix}.csv";
        using var writer = new StreamWriter(outputPath, false, new System.Text.UTF8Encoding(true));
        writer.WriteLine("Quote,Book,Author");
        foreach (var q in allQuotes)
        {
            string line = string.Join(",", EscapeCsv(q.Quote), EscapeCsv(q.Book), EscapeCsv(q.Author));
            writer.WriteLine(line);
        }

        Console.WriteLine($"\nTotal {allQuotes.Count} quotes fetched and saved to {outputPath}!");
    }

    static string EscapeCsv(string value)
    {
        if (value.Contains("\"") || value.Contains(",") || value.Contains("\n"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
