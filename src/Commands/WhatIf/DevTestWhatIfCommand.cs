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

        if (subscriptionId.GetValueOrDefault() == Guid.Empty)
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
        IEnumerable<UsageDetail> resources;


        await AnsiConsoleExt.Status()
            .StartAsync("Fetching cost data for resources...", async ctx =>
            {
                resources = await _costRetriever.RetrieveUsageDetails(
                    settings.Debug,
                    settings.GetScope,
                    "",
                    settings.From,
                    settings.To);

                // We need to group the resources by resource id AND product as we get for the same resource multiple items for each day
                // However, we do need to make sure we sum the quantity and cost
                resources = resources.OfType<LegacyUsageDetail>()
                    //   .Where(a => a.properties is
                    //       { consumedService: "Microsoft.Compute", meterDetails.meterCategory: "Virtual Machines" })
                    .GroupBy(a => a.Properties.ResourceId)
                    .Select(a => new LegacyUsageDetail
                    {
                        Id = a.Key,
                        Name = a.First().Name,
                        Type = a.First().Type,
                        Kind = a.First().Kind,
                        Tags = a.First().Tags,
                        Properties = new LegacyUsageDetailProperties
                        {
                            MeterDetails = a.First().Properties.MeterDetails != null
                                ? new MeterDetailsResponse()
                                {
                                    MeterCategory = a.First().Properties.MeterDetails.MeterCategory,
                                    UnitOfMeasure = a.First().Properties.MeterDetails.UnitOfMeasure,
                                    MeterName = a.First().Properties.MeterDetails.MeterName,
                                    MeterSubCategory = a.First().Properties.MeterDetails.MeterSubCategory,
                                }
                                : null,
                            Quantity = a.Sum(b => b.Properties.Quantity),
                            ConsumedService = a.First().Properties.ConsumedService,
                            Cost = a.Sum(b => b.Properties.Cost),
                            MeterId = a.First().Properties.MeterId,
                            ResourceGroup = a.First().Properties.ResourceGroup,
                            Frequency = a.First().Properties.Frequency,
                            Product = a.First().Properties.Product,
                            AdditionalInfo = a.First().Properties.AdditionalInfo,
                            BillingCurrency = a.First().Properties.BillingCurrency,
                            BillingProfileId = a.First().Properties.BillingProfileId,
                            OfferId = a.First().Properties.OfferId,
                            ChargeType = a.First().Properties.ChargeType,
                            ResourceLocation = a.First().Properties.ResourceLocation,
                            ResourceId = a.First().Properties.ResourceId,
                            ResourceName = a.First().Properties.ResourceName,
                            BillingProfileName = a.First().Properties.BillingProfileName,
                            UnitPrice = a.First().Properties.UnitPrice,
                            EffectivePrice = a.First().Properties.EffectivePrice,
                            BillingPeriodStartDate = a.First().Properties.BillingPeriodStartDate,
                            BillingPeriodEndDate = a.First().Properties.BillingPeriodEndDate,
                            PublisherType = a.First().Properties.PublisherType,
                            IsAzureCreditEligible = a.First().Properties.IsAzureCreditEligible,
                            SubscriptionName = a.First().Properties.SubscriptionName,
                            SubscriptionId = a.First().Properties.SubscriptionId,
                        }
                    });

                ctx.Status = "Running What-If analysis...";

                List<Task> tasks = new List<Task>();

                foreach (var resource in resources.OfType<LegacyUsageDetail>())
                {
                    var meterId = resource.Properties.MeterId;
                    var location = resource.Properties.ResourceLocation;
                    var currency = resource.Properties.BillingCurrency;

                    // Skip if any required parameter is missing
                    if (string.IsNullOrWhiteSpace(meterId) || string.IsNullOrWhiteSpace(location)) return;

                    var devTestPrice = await GetDevTestPrice(meterId, location, currency);

                    if (devTestPrice.HasValue) // && devTestPrice.Value < resource.properties.effectivePrice)
                    {
                        Console.WriteLine(
                            $"Resource ID {resource.Properties.ResourceId} could have saved {resource.Properties.Cost - devTestPrice} {currency} with DevTest pricing.");
                    }
                }

                // Wait for all tasks to complete
                // await Task.WhenAll(tasks);
            });


        return 0;
    }

    private async Task<double?> GetDevTestPrice(string meterId, string location, string currency)
    {
        // Use the service name, location, and currency as the cache key
        string cacheKey = $"{meterId}:{location}:{currency}";

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
                $"priceType eq 'DevTestConsumption' and Location eq '{location}' and meterId eq '{meterId}'";
            IEnumerable<PriceRecord> devTestPrices = await _priceRetriever.GetAzurePricesAsync(currency, filter);
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