using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI; // Needed for WebDriverWait
using SeleniumExtras.WaitHelpers; // Needed for ExpectedConditions (install SeleniumExtras.WaitHelpers NuGet package)
using System.Text; // For CSV encoding
using System.Threading; // Needed for Thread.Sleep

class Program
{
    // Use record for immutability and conciseness
    record QuoteRecord(string Quote, string Book, string Author);

    // Random number generator for delays
    private static Random random = new Random();

    static void Main(string[] args)
    {
        Console.WriteLine("Starting Selenium scraping...");

        var chromeService = ChromeDriverService.CreateDefaultService();
        chromeService.HideCommandPromptWindow = true;
        chromeService.SuppressInitialDiagnosticInformation = true;

        var chromeOptions = new ChromeOptions();

        // Keep existing Chrome options - they help avoid detection but aren't foolproof
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
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15)); // Increased wait time slightly

        string baseUrl = "https://1000kitap.com/nakkalAmca/alintilar";
        var allQuotes = new List<QuoteRecord>();
        int maxPage = 1;

        // --- Step 1: Find Max Page ---
        Console.WriteLine("Determining maximum page number...");
        try
        {
            driver.Navigate().GoToUrl(baseUrl + "?sayfa=1");
            Console.WriteLine("Loaded page 1 to check pagination.");
            wait.Until(ExpectedConditions.ElementExists(By.XPath("//div[contains(@class, 'mv-3') and contains(@class, 'justify-center')]")));
            Console.WriteLine("Pagination container found.");

            IWebElement maxPageElement = driver.FindElement(By.XPath("//div[contains(@class, 'mv-3') and contains(@class, 'justify-center')]//a[contains(@href, '?sayfa=')][last()-1]/span"));
            if (int.TryParse(maxPageElement.Text, out int pageNum))
            {
                maxPage = pageNum;
                Console.WriteLine($"Maximum page number found: {maxPage}");
            }
            else
            {
                Console.WriteLine($"Could not parse max page number ('{maxPageElement.Text}'), defaulting to 1.");
            }
        }
        catch (NoSuchElementException)
        {
            Console.WriteLine("Pagination control or max page element not found. Assuming only 1 page exists.");
            try
            {
                wait.Until(drv => drv.FindElements(By.XPath("//div[contains(@class, 'bg-ana') and contains(@class, 'pt-4') and .//div[contains(@class, 'bg-alinti-acik')]]")).Count >= 0);
                Console.WriteLine("Page 1 seems valid, proceeding with maxPage = 1.");
                maxPage = 1;
            }
            catch { maxPage = 0; Console.WriteLine("Page 1 also appears empty or invalid. Exiting."); }
        }
        catch (WebDriverTimeoutException ex) { maxPage = 1; Console.WriteLine($"Timeout waiting for pagination on page 1: {ex.Message}. Assuming 1 page."); }
        catch (Exception ex) { maxPage = 1; Console.WriteLine($"Unexpected error finding max page: {ex.Message}. Defaulting to page 1."); }


        // --- Step 2: Loop Through Pages ---
        for (int currentPage = 1; currentPage <= maxPage; currentPage++)
        {
            // *** ADD DELAY BEFORE NAVIGATING TO THE NEXT PAGE ***
            // Don't delay before the first page
            if (currentPage > 1)
            {
                // Wait for a random time between 2.5 and 5.5 seconds (adjust as needed)
                int delayMilliseconds = random.Next(2500, 5501);
                Console.WriteLine($"--- Waiting for {delayMilliseconds / 1000.0:F1} seconds before loading page {currentPage} ---");
                Thread.Sleep(delayMilliseconds);
            }

            string url = baseUrl + $"?sayfa={currentPage}";
            Console.WriteLine($"Navigating to page {currentPage}: {url}");

            try
            {
                driver.Navigate().GoToUrl(url);
                wait.Until(ExpectedConditions.ElementExists(By.XPath("//div[contains(@class, 'bg-ana') and contains(@class, 'pt-4') and .//div[contains(@class, 'bg-alinti-acik')]] | //div[contains(@class, 'text-center') and contains(@class, 'p-3')]")));
                Console.WriteLine($"Page {currentPage} loaded.");
            }
            catch (WebDriverTimeoutException ex)
            {
                Console.WriteLine($"Timeout waiting for elements on page {currentPage}: {ex.Message}. Skipping page.");
                continue;
            }
            // Catch potential navigation errors specifically
            catch (WebDriverException navEx)
            {
                Console.WriteLine($"Navigation error on page {currentPage}: {navEx.Message}");
                // Check if it's a rate limit error again
                if (driver.PageSource.Contains("Error 1015") || driver.PageSource.Contains("rate limited"))
                {
                    Console.WriteLine("Rate limit detected during navigation. Stopping script.");
                    break; // Exit the loop
                }
                Console.WriteLine("Attempting to continue to the next page...");
                continue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error navigating to or loading page {currentPage}: {ex.Message}");
                Console.WriteLine("Attempting to continue to the next page...");
                continue;
            }

            Console.WriteLine("Finding quote containers...");
            IList<IWebElement> postContainers = driver.FindElements(By.XPath("//div[contains(@class, 'bg-ana') and contains(@class, 'pt-4') and .//div[contains(@class, 'bg-alinti-acik')]]"));

            if (postContainers == null || postContainers.Count == 0)
            {
                Console.WriteLine($"No quote containers found on page {currentPage}. Moving to next page or finishing.");
                continue;
            }

            Console.WriteLine($"Found {postContainers.Count} quote containers on page {currentPage}. Processing...");
            int quotesAddedFromPage = 0;
            foreach (var container in postContainers)
            {
                string quote = ""; string book = ""; string author = "";
                try { quote = container.FindElement(By.XPath(".//div[contains(@class, 'bg-alinti-acik')]//span[contains(@class, 'text-16')]")).Text.Trim(); } catch (NoSuchElementException) { } catch (Exception ex) { Console.WriteLine($" - Error getting quote text: {ex.Message}"); }
                try { book = container.FindElement(By.XPath(".//div[contains(@class, 'flex-1') and contains(@class, 'flex-column')]/a[1]")).Text.Trim(); } catch (NoSuchElementException) { } catch (Exception ex) { Console.WriteLine($" - Error getting book title: {ex.Message}"); }
                try { author = container.FindElement(By.XPath(".//div[contains(@class, 'flex-1') and contains(@class, 'flex-column')]/a[2]")).Text.Trim(); } catch (NoSuchElementException) { } catch (Exception ex) { Console.WriteLine($" - Error getting author name: {ex.Message}"); }

                if (!string.IsNullOrWhiteSpace(quote))
                {
                    allQuotes.Add(new QuoteRecord(quote, book, author));
                    quotesAddedFromPage++;
                }
                // Optional small delay between processing items on a page, might be overkill
                // Thread.Sleep(random.Next(50, 201));
            }
            Console.WriteLine($"Added {quotesAddedFromPage} quotes from page {currentPage}.");
        }

        // --- Step 3: Save to CSV ---
        Console.WriteLine("\nFinished scraping pages. Writing data to CSV file...");
        string dateSuffix = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        string outputPath = $"1000kitap_nakkalAmca_quotes_{dateSuffix}.csv";

        try
        {
            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
            writer.WriteLine("Quote,Book,Author");
            foreach (var q in allQuotes)
            {
                string line = string.Join(",", EscapeCsv(q.Quote), EscapeCsv(q.Book), EscapeCsv(q.Author));
                writer.WriteLine(line);
            }
            Console.WriteLine($"\nTotal {allQuotes.Count} quotes fetched and saved to {outputPath}!");
        }
        catch (IOException ioEx) { Console.WriteLine($"\nError writing CSV file '{outputPath}'. Details: {ioEx.Message}"); }
        catch (Exception ex) { Console.WriteLine($"\nAn unexpected error occurred while writing the CSV file: {ex.Message}"); }

        Console.WriteLine("Scraping complete. ChromeDriver will now close.");
    }

    static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) { return "\"\""; }
        if (value.Contains("\"") || value.Contains(",") || value.Contains("\n") || value.Contains("\r"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return $"\"{value}\""; // Quote all fields for consistency
    }
}
