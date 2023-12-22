using System.Collections.Concurrent;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.WhatIf;

// Run what-if scenarios to check price difference if the resources would have run in a different region
public class RegionWhatIfCommand : AsyncCommand<WhatIfSettings>
{
    private readonly IPriceRetriever _priceRetriever;
    private readonly ICostRetriever _costRetriever;

    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = new();
    
  
    public RegionWhatIfCommand(IPriceRetriever priceRetriever, ICostRetriever costRetriever)
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
        Dictionary<UsageDetail, List<PriceRecord>> pricesByRegion = new();

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
                    .Where(a => a.Properties is
                        { ConsumedService: "Microsoft.Compute", MeterDetails.MeterCategory: "Virtual Machines" })
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
                    string skuName = resource.Properties.MeterDetails.MeterName;
                    ctx.Status = "Fetching prices for " + skuName;

                    var items = await FetchPricesForAllRegions(skuName, resource.Properties.MeterId,
                        resource.Properties.BillingCurrency);

                    pricesByRegion.Add(resource, items.ToList());
                }
            });

        // Write the output
        await _outputFormatters[settings.Output]
            .WritePricesPerRegion(settings, pricesByRegion);

        return 0;
    }

    private Dictionary<string, IEnumerable<PriceRecord>> _priceCache = new();

    private async Task<IEnumerable<PriceRecord>> FetchPricesForAllRegions(string skuName, string meterId,
        string currency = "USD")
    {
        // Cachekey
        var cacheKey = skuName + ":" + meterId + ":" + currency;

        // Check if prices for the given SKU name exist in the cache
        if (_priceCache.TryGetValue(cacheKey, out var regions))
        {
            return regions;
        }

        string filter = $"serviceName eq 'Virtual Machines' and skuName eq '{skuName}' and type eq 'Consumption'";
        IEnumerable<PriceRecord> prices = await _priceRetriever.GetAzurePricesAsync(currency, filter);

        // find the item by meterId and use that to determine the actual product name
        // if we do not do that, we end up with both windows and linux machines
        var actualItem = prices.FirstOrDefault(a => a.MeterId == meterId);

        if (actualItem is not null)
            prices = prices.Where(a => a.ProductName == actualItem.ProductName);

        // Store the fetched prices in the cache
        _priceCache[cacheKey] = prices;

        return prices;
    }
}