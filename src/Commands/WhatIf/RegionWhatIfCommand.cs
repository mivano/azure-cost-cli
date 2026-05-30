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
    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = OutputFormatterFactory.Create();

    public RegionWhatIfCommand(IPriceRetriever priceRetriever, ICostRetriever costRetriever)
    {
        _priceRetriever = priceRetriever;
        _costRetriever = costRetriever;
    }

    protected override ValidationResult Validate(CommandContext context, WhatIfSettings settings)
    {
        return CommandHelpers.ValidateAndResolveSubscription(
            settings.Subscription, settings.GetScope.IsSubscriptionBased,
            id => settings.Subscription = id);
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, WhatIfSettings settings, CancellationToken cancellationToken)
    {
        _costRetriever.CostApiAddress = settings.CostApiAddress;
        _priceRetriever.PriceApiAddress = settings.PriceApiAddress;

        // Fetch the costs from the Azure Cost Management API
        IEnumerable<UsageDetails> resources;
        Dictionary<UsageDetails, List<PriceRecord>> pricesByRegion = new();

        await AnsiConsoleExt.StatusAsync(settings.Quiet, "Fetching cost data for resources...", async ctx =>
            {
                resources = await _costRetriever.RetrieveUsageDetails(
                    settings.Debug,
                    settings.GetScope,
                    "",
                    settings.GetFromDate(),
                    settings.GetToDate());

                // We need to group the resources by resource id AND product as we get for the same resource multiple items for each day
                // However, we do need to make sure we sum the quantity and cost
                resources = resources
                    .Where(a => a.properties is
                        { consumedService: "Microsoft.Compute", meterDetails.meterCategory: "Virtual Machines" })
                    .GroupBy(a => a.properties.resourceId)
                    .Select(a =>
                    {
                        var first = a.First();
                        return new UsageDetails
                        {
                            id = a.Key,
                            name = first.name,
                            type = first.type,
                            kind = first.kind,
                            tags = first.tags,
                            properties = new UsageProperties
                            {
                                meterDetails = new MeterDetails
                                {
                                    meterCategory = first.properties.meterDetails.meterCategory,
                                    unitOfMeasure = first.properties.meterDetails.unitOfMeasure,
                                    meterName = first.properties.meterDetails.meterName,
                                    meterSubCategory = first.properties.meterDetails.meterSubCategory,
                                },
                                quantity = a.Sum(b => b.properties.quantity),
                                consumedService = first.properties.consumedService,
                                cost = a.Sum(b => b.properties.cost),
                                meterId = first.properties.meterId,
                                resourceGroup = first.properties.resourceGroup,
                                frequency = first.properties.frequency,
                                product = first.properties.product,
                                additionalInfo = first.properties.additionalInfo,
                                billingCurrency = first.properties.billingCurrency,
                                billingProfileId = first.properties.billingProfileId,
                                offerId = first.properties.offerId,
                                chargeType = first.properties.chargeType,
                                resourceLocation = first.properties.resourceLocation,
                                resourceId = first.properties.resourceId,
                                resourceName = first.properties.resourceName,
                                billingProfileName = first.properties.billingProfileName,
                                unitPrice = first.properties.unitPrice,
                                effectivePrice = first.properties.effectivePrice,
                                billingPeriodStartDate = first.properties.billingPeriodStartDate,
                                billingPeriodEndDate = first.properties.billingPeriodEndDate,
                                publisherType = first.properties.publisherType,
                                isAzureCreditEligible = first.properties.isAzureCreditEligible,
                                subscriptionName = first.properties.subscriptionName,
                                subscriptionId = first.properties.subscriptionId,
                            }
                        };
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