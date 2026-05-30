using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.WhatIf;

// Run what-if scenarios to check price difference if the resources were on a DevTest subscription
public class DevTestWhatIfCommand : AsyncCommand<WhatIfSettings>
{
    private readonly IPriceRetriever _priceRetriever;
    private readonly ICostRetriever _costRetriever;
    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = OutputFormatterFactory.Create();

    public DevTestWhatIfCommand(IPriceRetriever priceRetriever, ICostRetriever costRetriever)
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

        IEnumerable<UsageDetails> resources = Enumerable.Empty<UsageDetails>();
        List<DevTestComparisonItem> comparisonItems = new();

        await AnsiConsoleExt.StatusAsync(settings.Quiet, "Fetching usage details...", async ctx =>
            {
                resources = await _costRetriever.RetrieveUsageDetails(
                    settings.Debug,
                    settings.GetScope,
                    "",
                    settings.GetFromDate(),
                    settings.GetToDate());

                // Group by resource ID + meterId, summing quantity and cost
                resources = resources
                    .Where(a => a.properties is { consumedService: "Microsoft.Compute", meterDetails.meterCategory: "Virtual Machines" })
                    .GroupBy(a => new { a.properties.resourceId, a.properties.meterId })
                    .Select(a =>
                    {
                        var first = a.First();
                        return new UsageDetails
                        {
                            id = first.id,
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

                ctx.Status = "Fetching DevTest prices...";

                foreach (var resource in resources)
                {
                    string skuName = resource.properties.meterDetails.meterName;
                    ctx.Status = $"Fetching DevTest prices for {skuName}";

                    var devTestPrices = await FetchDevTestPrices(skuName, resource.properties.meterId,
                        resource.properties.billingCurrency);

                    // Find the DevTest price for the resource's current region
                    var devTestPrice = devTestPrices
                        .FirstOrDefault(p => string.Equals(p.ArmRegionName, resource.properties.resourceLocation, StringComparison.OrdinalIgnoreCase));

                    double currentCost = resource.properties.cost;
                    double? devTestUnitPrice = devTestPrice?.RetailPrice;
                    double? devTestCost = devTestUnitPrice.HasValue ? resource.properties.quantity * devTestUnitPrice.Value : null;
                    double? savings = devTestCost.HasValue ? currentCost - devTestCost.Value : null;
                    double? savingsPct = savings.HasValue && currentCost > 0 ? savings.Value / currentCost * 100 : null;

                    comparisonItems.Add(new DevTestComparisonItem(
                        ResourceName: resource.properties.resourceName ?? resource.name,
                        ResourceGroup: resource.properties.resourceGroup ?? "",
                        Product: resource.properties.product ?? "",
                        MeterName: skuName ?? "",
                        Region: resource.properties.resourceLocation ?? "",
                        Currency: resource.properties.billingCurrency ?? "USD",
                        UnitOfMeasure: resource.properties.meterDetails?.unitOfMeasure ?? "",
                        Quantity: resource.properties.quantity,
                        CurrentUnitPrice: resource.properties.effectivePrice,
                        CurrentCost: currentCost,
                        DevTestUnitPrice: devTestUnitPrice,
                        DevTestCost: devTestCost,
                        Savings: savings,
                        SavingsPercentage: savingsPct));
                }
            });

        await _outputFormatters[settings.Output]
            .WriteDevTestComparison(settings, comparisonItems);

        return 0;
    }

    private readonly Dictionary<string, IEnumerable<PriceRecord>> _priceCache = new();

    private async Task<IEnumerable<PriceRecord>> FetchDevTestPrices(string skuName, string meterId,
        string currency = "USD")
    {
        var cacheKey = $"{skuName}:{meterId}:{currency}";

        if (_priceCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        // Fetch DevTest (priceType=DevTestConsumption) prices for the same SKU
        string filter = $"serviceName eq 'Virtual Machines' and skuName eq '{skuName}' and type eq 'DevTestConsumption'";
        IEnumerable<PriceRecord> prices = await _priceRetriever.GetAzurePricesAsync(currency, filter);

        // Match by meterId to find the correct product (avoids mixing Windows/Linux)
        var actualItem = prices.FirstOrDefault(a => a.MeterId == meterId);

        if (actualItem is not null)
            prices = prices.Where(a => a.ProductName == actualItem.ProductName);

        _priceCache[cacheKey] = prices;

        return prices;
    }
}