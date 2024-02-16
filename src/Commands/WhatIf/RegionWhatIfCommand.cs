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
        IEnumerable<UsageDetails> resources;
        Dictionary<UsageDetails, List<PriceRecord>> pricesByRegion = new();

        await AnsiConsoleExt.Status()
            .StartAsync("Fetching cost data for resources...", async ctx =>
            {
                resources = await _costRetriever.RetrieveUsageDetails(
                    settings.Debug,
                    settings.GetScope,
                    "",
                    settings.From,
                    settings.To,
                    new SecurityCredentials(settings.TenantId, settings.ServicePrincipalId, settings.ServicePrincipalSecret));

                // We need to group the resources by resource id AND product as we get for the same resource multiple items for each day
                // However, we do need to make sure we sum the quantity and cost
                resources = resources
                    .Where(a => a.properties is
                        { consumedService: "Microsoft.Compute", meterDetails.meterCategory: "Virtual Machines" })
                    .GroupBy(a => a.properties.resourceId)
                    .Select(a => new UsageDetails
                    {
                        id = a.Key,
                        name = a.First().name,
                        type = a.First().type,
                        kind = a.First().kind,
                        tags = a.First().tags,
                        properties = new UsageProperties
                        {
                            meterDetails = new MeterDetails
                            {
                                meterCategory = a.First().properties.meterDetails.meterCategory,
                                unitOfMeasure = a.First().properties.meterDetails.unitOfMeasure,
                                meterName = a.First().properties.meterDetails.meterName,
                                meterSubCategory = a.First().properties.meterDetails.meterSubCategory,
                            },
                            quantity = a.Sum(b => b.properties.quantity),
                            consumedService = a.First().properties.consumedService,
                            cost = a.Sum(b => b.properties.cost),
                            meterId = a.First().properties.meterId,
                            resourceGroup = a.First().properties.resourceGroup,
                            frequency = a.First().properties.frequency,
                            product = a.First().properties.product,
                            additionalInfo = a.First().properties.additionalInfo,
                            billingCurrency = a.First().properties.billingCurrency,
                            billingProfileId = a.First().properties.billingProfileId,
                            offerId = a.First().properties.offerId,
                            chargeType = a.First().properties.chargeType,
                            resourceLocation = a.First().properties.resourceLocation,
                            resourceId = a.First().properties.resourceId,
                            resourceName = a.First().properties.resourceName,
                            billingProfileName = a.First().properties.billingProfileName,
                            unitPrice = a.First().properties.unitPrice,
                            effectivePrice = a.First().properties.effectivePrice,
                            billingPeriodStartDate = a.First().properties.billingPeriodStartDate,
                            billingPeriodEndDate = a.First().properties.billingPeriodEndDate,
                            publisherType = a.First().properties.publisherType,
                            isAzureCreditEligible = a.First().properties.isAzureCreditEligible,
                            subscriptionName = a.First().properties.subscriptionName,
                            subscriptionId = a.First().properties.subscriptionId,
                        }
                    });

                ctx.Status = "Running What-If analysis...";

                List<Task> tasks = new List<Task>();

                foreach (var resource in resources)
                {
                    string skuName = resource.properties.meterDetails.meterName;
                    ctx.Status = "Fetching prices for " + skuName;

                    var items = await FetchPricesForAllRegions(skuName, resource.properties.meterId,
                        resource.properties.billingCurrency);

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