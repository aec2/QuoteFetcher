using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI; // Needed for WebDriverWait
using SeleniumExtras.WaitHelpers; // Needed for ExpectedConditions (install SeleniumExtras.WaitHelpers NuGet package)
using System.Text; // For CSV encoding
using System.Threading; // Keep for potential small delays if needed, but prefer WebDriverWait

class Program
{
    // Use record for immutability and conciseness
    record QuoteRecord(string Quote, string Book, string Author);

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
        // Ensure directory is clean or handled appropriately if needed between runs
        // For simplicity here, we just ensure it exists. Consider deleting/recreating if state issues arise.
        Directory.CreateDirectory(tempProfileDir);

        chromeOptions.AddArgument($"--user-data-dir={tempProfileDir}");
        chromeOptions.AddArgument("--remote-debugging-port=9222"); // Can be useful for debugging, keep if needed
        chromeOptions.AddArgument("--disable-extensions");
        chromeOptions.AddArgument("--disable-gpu");
        chromeOptions.AddArgument("--no-sandbox"); // Often necessary in containerized environments
        chromeOptions.AddArgument("--disable-dev-shm-usage"); // Overcomes limited resource problems
        chromeOptions.AddArgument("--disable-blink-features=AutomationControlled"); // Attempt to hide automation
        chromeOptions.AddExcludedArgument("enable-automation"); // Remove "Chrome is being controlled..."
        chromeOptions.AddAdditionalOption("useAutomationExtension", false); // Don't use the automation extension

        Console.WriteLine("Launching ChromeDriver...");
        using var driver = new ChromeDriver(chromeService, chromeOptions);
        // Instantiate WebDriverWait - adjust timeout as needed (e.g., 15 seconds)
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));

        string baseUrl = "https://1000kitap.com/nakkalAmca/alintilar"; // Base URL without page param
        var allQuotes = new List<QuoteRecord>();
        int maxPage = 1; // Default to 1 page

        // --- Step 1: Find Max Page ---
        Console.WriteLine("Determining maximum page number...");
        try
        {
            // Navigate to page 1 to ensure pagination control is present
            driver.Navigate().GoToUrl(baseUrl + "?sayfa=1");
            Console.WriteLine("Loaded page 1 to check pagination.");

            // Wait for the pagination container element to be present
            // Using a more specific XPath for the pagination container
            wait.Until(ExpectedConditions.ElementExists(By.XPath("//div[contains(@class, 'mv-3') and contains(@class, 'justify-center')]")));
            Console.WriteLine("Pagination container found.");

            // Find the second-to-last page link's span which usually holds the max page number
            // XPath: Find the div containing pagination links, then find 'a' tags with '?sayfa=',
            // select the one before the last one ([last()-1]), and get its child 'span'.
            IWebElement maxPageElement = driver.FindElement(By.XPath("//div[contains(@class, 'mv-3') and contains(@class, 'justify-center')]//a[contains(@href, '?sayfa=')][last()-1]/span"));
            if (int.TryParse(maxPageElement.Text, out int pageNum))
            {
                maxPage = pageNum;
                Console.WriteLine($"Maximum page number found: {maxPage}");
            }
            else
            {
                Console.WriteLine($"Could not parse max page number ('{maxPageElement.Text}'), defaulting to 1.");
                // Keep maxPage = 1
            }
        }
        catch (NoSuchElementException)
        {
            // If pagination or the specific link isn't found, assume it's just one page.
            Console.WriteLine("Pagination control or max page element not found. Assuming only 1 page exists.");
            // Check if *any* quotes exist on page 1 to confirm it's valid
            try
            {
                // Wait briefly to see if any quote container appears on page 1
                wait.Until(drv => drv.FindElements(By.XPath("//div[contains(@class, 'bg-ana') and contains(@class, 'pt-4') and .//div[contains(@class, 'bg-alinti-acik')]]")).Count >= 0);
                Console.WriteLine("Page 1 seems valid (might have quotes or be empty), proceeding with maxPage = 1.");
                maxPage = 1; // Ensure it's set to 1
            }
            catch
            {
                Console.WriteLine("Page 1 also appears empty or invalid after waiting. Exiting.");
                maxPage = 0; // Prevent loop execution
            }
        }
        catch (WebDriverTimeoutException ex)
        {
            Console.WriteLine($"Timeout waiting for pagination on page 1: {ex.Message}. Assuming 1 page.");
            maxPage = 1; // Assume 1 page if pagination times out
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error finding max page: {ex.Message}. Defaulting to page 1.");
            // Decide if you want to proceed with page 1 or stop
            maxPage = 1;
        }


        // --- Step 2: Loop Through Pages ---
        for (int currentPage = 1; currentPage <= maxPage; currentPage++)
        {
            // Construct URL correctly for page 1 and subsequent pages
            string url = baseUrl + $"?sayfa={currentPage}";
            Console.WriteLine($"Navigating to page {currentPage}: {url}");

            try
            {
                driver.Navigate().GoToUrl(url);
                // Wait for the main content area or specifically the quote containers to be present
                // Waiting for at least one container is a good indicator the page content started loading
                wait.Until(ExpectedConditions.ElementExists(By.XPath("//div[contains(@class, 'bg-ana') and contains(@class, 'pt-4') and .//div[contains(@class, 'bg-alinti-acik')]] | //div[contains(@class, 'text-center') and contains(@class, 'p-3')]")));
                // The second part `| //div[contains(@class, 'text-center') ...]` handles the case where the page might load but have no quotes, showing the footer instead.

                Console.WriteLine($"Page {currentPage} loaded.");
            }
            catch (WebDriverTimeoutException ex)
            {
                Console.WriteLine($"Timeout waiting for elements on page {currentPage}: {ex.Message}. Skipping page.");
                continue; // Skip to next page
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error navigating to or loading page {currentPage}: {ex.Message}");
                Console.WriteLine("Attempting to continue to the next page...");
                continue; // Skip to next page
            }

            // Find all quote containers using the correct XPath
            Console.WriteLine("Finding quote containers...");
            // Use FindElements to get all matching elements on the page
            // XPath: Find divs with 'bg-ana' and 'pt-4' that contain a descendant div with 'bg-alinti-acik'
            IList<IWebElement> postContainers = driver.FindElements(By.XPath("//div[contains(@class, 'bg-ana') and contains(@class, 'pt-4') and .//div[contains(@class, 'bg-alinti-acik')]]"));

            if (postContainers == null || postContainers.Count == 0)
            {
                Console.WriteLine($"No quote containers found on page {currentPage}. Moving to next page or finishing.");
                continue; // Go to the next iteration of the loop
            }

            Console.WriteLine($"Found {postContainers.Count} quote containers on page {currentPage}. Processing...");
            int quotesAddedFromPage = 0;
            foreach (var container in postContainers)
            {
                string quote = "";
                string book = "";
                string author = "";

                // Extract data using relative XPaths and robust try-catch blocks for each piece
                try
                {
                    // XPath: Find descendant div with 'bg-alinti-acik', then any descendant span with 'text-16'
                    quote = container.FindElement(By.XPath(".//div[contains(@class, 'bg-alinti-acik')]//span[contains(@class, 'text-16')]")).Text.Trim();
                }
                catch (NoSuchElementException) { /* Console.WriteLine(" - Quote text not found in a container."); */ } // Reduce noise
                catch (Exception ex) { Console.WriteLine($" - Error getting quote text: {ex.Message}"); }

                try
                {
                    // XPath: Find descendant div with 'flex-1' and 'flex-column', then its first 'a' child
                    book = container.FindElement(By.XPath(".//div[contains(@class, 'flex-1') and contains(@class, 'flex-column')]/a[1]")).Text.Trim();
                }
                catch (NoSuchElementException) { /* Console.WriteLine(" - Book title not found in a container."); */ } // Reduce noise
                catch (Exception ex) { Console.WriteLine($" - Error getting book title: {ex.Message}"); }

                try
                {
                    // XPath: Find descendant div with 'flex-1' and 'flex-column', then its second 'a' child
                    author = container.FindElement(By.XPath(".//div[contains(@class, 'flex-1') and contains(@class, 'flex-column')]/a[2]")).Text.Trim();
                }
                catch (NoSuchElementException) { /* Console.WriteLine(" - Author name not found in a container."); */ } // Reduce noise
                catch (Exception ex) { Console.WriteLine($" - Error getting author name: {ex.Message}"); }

                // Add record only if quote is found (most essential part)
                if (!string.IsNullOrWhiteSpace(quote))
                {
                    allQuotes.Add(new QuoteRecord(quote, book, author));
                    quotesAddedFromPage++;
                    // Console.WriteLine($"  Added: {quote.Substring(0, Math.Min(quote.Length, 50))}... ({book} by {author})"); // Optional: Log snippet
                }
                else
                {
                    // Console.WriteLine($" - Skipping container as essential quote text was missing."); // Reduce noise
                }
            }
            Console.WriteLine($"Added {quotesAddedFromPage} quotes from page {currentPage}.");

            // Optional: Add a small delay between pages if needed to be polite to the server
            // Thread.Sleep(500); // e.g., 500 milliseconds
        }

        // --- Step 3: Save to CSV ---
        Console.WriteLine("\nFinished scraping pages. Writing data to CSV file...");
        string dateSuffix = DateTime.Now.ToString("yyyy-MM-dd_HHmmss"); // Add time for uniqueness
        string outputPath = $"1000kitap_nakkalAmca_quotes_{dateSuffix}.csv";

        try
        {
            // Use StreamWriter with UTF8 encoding (with BOM is default for this constructor)
            using var writer = new StreamWriter(outputPath, false, Encoding.UTF8);
            // Write Header
            writer.WriteLine("Quote,Book,Author"); // Header literals don't need escaping

            // Write Data Rows
            foreach (var q in allQuotes)
            {
                // Use the robust EscapeCsv function for each field
                string line = string.Join(",", EscapeCsv(q.Quote), EscapeCsv(q.Book), EscapeCsv(q.Author));
                writer.WriteLine(line);
            }
            Console.WriteLine($"\nTotal {allQuotes.Count} quotes fetched and saved to {outputPath}!");
        }
        catch (IOException ioEx)
        {
            // Handle potential file access errors
            Console.WriteLine($"\nError writing CSV file '{outputPath}'. It might be open in another program or you lack permissions. Details: {ioEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nAn unexpected error occurred while writing the CSV file: {ex.Message}");
        }

        Console.WriteLine("Scraping complete. ChromeDriver will now close.");
        // The 'using' statement for the driver handles disposal/closing automatically.
    }

    // Helper function to correctly format strings for CSV, handling quotes, commas, and newlines
    static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            // Represent empty or null string as an empty quoted field ""
            return "\"\"";
        }
        // Check if the value contains characters that require quoting in CSV
        if (value.Contains("\"") || value.Contains(",") || value.Contains("\n") || value.Contains("\r"))
        {
            // Escape existing double quotes by replacing them with two double quotes ""
            // and enclose the entire value in double quotes.
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        // If no special characters are found, enclose in quotes for consistency (optional but safer)
        return $"\"{value}\"";
    }
}
