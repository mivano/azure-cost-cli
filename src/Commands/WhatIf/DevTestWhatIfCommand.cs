using System.Collections.Concurrent;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.WhatIf;

public class DevTestWhatIfCommand : AsyncCommand<WhatIfSettings>
{
    private readonly IPriceRetriever _priceRetriever;
    private readonly ICostRetriever _costRetriever;

    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = new();

    private ConcurrentDictionary<string, CacheEntry> _cache = new();
    private ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private TimeSpan _cacheLifetime = TimeSpan.FromHours(1); // Cache lifetime can be adjusted as needed

    public DevTestWhatIfCommand(IPriceRetriever priceRetriever, ICostRetriever costRetriever)
    {
        _priceRetriever = priceRetriever;
        _costRetriever = costRetriever;

        // Add the output formatters
        _outputFormatters.Add(OutputFormat.Console, new ConsoleOutputFormatter());
        _outputFormatters.Add(OutputFormat.Json, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Jsonc, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Text, new TextOutputFormatter());
        _outputFormatters.Add(OutputFormat.Markdown, new MarkdownOutputFormatter());
        _outputFormatters.Add(OutputFormat.Csv, new CsvOutputFormatter());
    }

    public override async Task<int> ExecuteAsync(CommandContext context, WhatIfSettings settings)
    {
        // Get the subscription ID from the settings
        var subscriptionId = settings.Subscription;

        if (subscriptionId.HasValue == false && (settings.GetScope.IsSubscriptionBased))
        {
            // Get the subscription ID from the Azure CLI
            try
            {
                if (settings.Debug)
                    AnsiConsole.WriteLine(
                        "No subscription ID specified. Trying to retrieve the default subscription ID from Azure CLI.");

                subscriptionId = Guid.Parse(AzCommand.GetDefaultAzureSubscriptionId());

                if (settings.Debug)
                    AnsiConsole.WriteLine($"Default subscription ID retrieved from az cli: {subscriptionId}");

                settings.Subscription = subscriptionId;
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(new ArgumentException(
                    "Missing subscription ID. Please specify a subscription ID or login to Azure CLI.", e));
                return -1;
            }
        }

        // Fetch the costs from the Azure Cost Management API
        IEnumerable<CostResourceItem> resources = new List<CostResourceItem>();



        await AnsiConsoleExt.Status()
            .StartAsync("Fetching cost data for resources...", async ctx =>
            {
                resources = await _costRetriever.RetrieveCostForResources(
                    settings.Debug,
                    settings.GetScope, settings.Filter,
                    settings.Metric,
                    false,
                    settings.Timeframe,
                    settings.From,
                    settings.To);

                ctx.Status = "Running What-If analysis...";

                List<Task> tasks = new List<Task>();
                
                foreach (var resource in resources)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        var serviceName = resource.ServiceName;
                        var location = resource.ResourceLocation;
                        var currency = resource.Currency;

                        // Skip if any required parameter is missing
                        if (string.IsNullOrWhiteSpace(serviceName) || string.IsNullOrWhiteSpace(location)) return;

                        var devTestPrice = await GetDevTestPrice(serviceName, location, currency);

                        if (devTestPrice.HasValue) // && devTestPrice < resource.Cost)
                        {
                            Console.WriteLine($"Resource ID {resource.ResourceId} could have saved {resource.Cost - devTestPrice} {currency} with DevTest pricing.");
                        }
                    }));
                }

                // Wait for all tasks to complete
                await Task.WhenAll(tasks);
                
            });


        return 0;
    }

    private async Task<double?> GetDevTestPrice(string serviceName, string location, string currency)
    {
        // Use the service name, location, and currency as the cache key
        string cacheKey = $"{serviceName}:{location}:{currency}";

        // Check if the cache entry exists and if it's not expired
        if (_cache.TryGetValue(cacheKey, out CacheEntry cacheEntry) && cacheEntry.Expiry > DateTime.Now)
        {
            return cacheEntry.Price;
        }

        // Get or create a new lock for this cache key
        SemaphoreSlim mylock = _locks.GetOrAdd(cacheKey, k => new SemaphoreSlim(1, 1));

        // Use the semaphore to ensure only one thread at a time can update a given cache entry
        await mylock.WaitAsync();

        try
        {
            // Check the cache again, in case another thread updated the entry while this thread was waiting for the lock
            if (_cache.TryGetValue(cacheKey, out cacheEntry) && cacheEntry.Expiry > DateTime.Now)
            {
                return cacheEntry.Price;
            }

            // If the price is not in the cache or it's expired, get it from the API
            string filter =
                $"priceType eq 'DevTestConsumption' and Location eq '{location}' and serviceName eq '{serviceName}'";
            IEnumerable<PriceRecord> devTestPrices = await _priceRetriever.GetAzurePricesAsync(filter);
            var devTestPriceRecord = devTestPrices.FirstOrDefault();
            double? price = devTestPriceRecord?.RetailPrice;

            // Store the price in the cache with an expiry time
            _cache[cacheKey] = new CacheEntry { Price = price, Expiry = DateTime.Now.Add(_cacheLifetime) };

            // Return the price, or null if there is no DevTest price
            return price;
        }
        finally
        {
            mylock.Release();
        }
    }
}


public class CacheEntry
{
    public double? Price { get; set; }
    public DateTime Expiry { get; set; }
}