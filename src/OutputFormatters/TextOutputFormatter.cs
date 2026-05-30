using System.Globalization;
using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.Commands.Budgets;
using AzureCostCli.Commands.CostByResource;
using AzureCostCli.Commands.CostByTag;
using AzureCostCli.Commands.DailyCost;
using AzureCostCli.Commands.DetectAnomaly;
using AzureCostCli.Commands.Diff;
using AzureCostCli.Commands.Regions;
using AzureCostCli.Commands.Threshold;
using AzureCostCli.Commands.WhatIf;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;

namespace AzureCostCli.OutputFormatters;

public class TextOutputFormatter : BaseOutputFormatter
{
    public override Task WriteAccumulatedCost(AccumulatedCostSettings settings, AccumulatedCostDetails accumulatedCostDetails)
    {
        if (accumulatedCostDetails.Costs.Any() == false)
        {
            Console.WriteLine("Azure Cost Overview");
            Console.WriteLine();
            Console.WriteLine("No data found");
            return Task.CompletedTask;
        }

        var output = new
        {
            costs = new
            {
                todaysCost = accumulatedCostDetails.Costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow)).Sum(a => settings.UseUSD ? a.CostUsd :  a.Cost),
                yesterdayCost = accumulatedCostDetails.Costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))
                    .Sum(a => settings.UseUSD ? a.CostUsd :  a.Cost),
                lastSevenDaysCost = accumulatedCostDetails.Costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)))
                    .Sum(a => settings.UseUSD ? a.CostUsd :  a.Cost),
                lastThirtyDaysCost = accumulatedCostDetails.Costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)))
                    .Sum(a => settings.UseUSD ? a.CostUsd :  a.Cost),
                totalCostInTimeframe = accumulatedCostDetails.Costs.Sum(a => settings.UseUSD ? a.CostUsd : a.Cost)
            },
        };

        var currency =settings.UseUSD ? "USD" : accumulatedCostDetails.Costs.FirstOrDefault()?.Currency;
        
        Console.WriteLine(
            $"Azure Cost Overview for {accumulatedCostDetails.Subscription.displayName} from {accumulatedCostDetails.Costs.Min(a => a.Date)} to {accumulatedCostDetails.Costs.Max(a => a.Date)}");
        Console.WriteLine();
        Console.WriteLine("Totals:");
        Console.WriteLine($"  Today: {output.costs.todaysCost:N2} {currency}");
        Console.WriteLine($"  Yesterday: {output.costs.yesterdayCost:N2} {currency}");
        Console.WriteLine($"  Last 7 days: {output.costs.lastSevenDaysCost:N2} {currency}");
        Console.WriteLine($"  Last 30 days: {output.costs.lastThirtyDaysCost:N2} {currency}");
        Console.WriteLine($"  Total cost in timeframe: {output.costs.totalCostInTimeframe:N2} {currency}");

        Console.WriteLine();
        Console.WriteLine("By Service Name:");
        foreach (var cost in accumulatedCostDetails.ByServiceNameCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"  {cost.ItemName}: {(settings.UseUSD ? cost.CostUsd :  cost.Cost):N2} {currency}");
        }

        Console.WriteLine();
        Console.WriteLine("By Location:");
        foreach (var cost in accumulatedCostDetails.ByLocationCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"  {cost.ItemName}: {(settings.UseUSD ? cost.CostUsd :  cost.Cost):N2} {currency}");
        }

        if (accumulatedCostDetails.BySubscriptionCosts != null)
        {
            Console.WriteLine();
            Console.WriteLine("By Subscriptions:");
            foreach (var cost in accumulatedCostDetails.BySubscriptionCosts.TrimList(threshold: settings.OthersCutoff))
            {
                Console.WriteLine($"  {cost.ItemName}: {(settings.UseUSD ? cost.CostUsd :  cost.Cost):N2} {currency}");
            }
        }

        if (settings.GetScope.IsSubscriptionBased)
        {
            Console.WriteLine();
            Console.WriteLine("By Resource Group:");
            foreach (var cost in accumulatedCostDetails.ByResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
            {
                Console.WriteLine($"  {cost.ItemName}: {(settings.UseUSD ? cost.CostUsd : cost.Cost):N2} {currency}");
            }
        }

        return Task.CompletedTask;
    }

    public override Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources,
        int totalCount = 0, double totalCost = 0, string currency = "USD")
    {
        if (settings.SkipHeader == false)
        {
            Console.WriteLine(
                $"Azure Cost Overview for {settings.Subscription} by resource");

            Console.WriteLine();
        }

        foreach (var resource in resources)
        {
            if (settings.UseUSD)
            {
                Console.WriteLine(
                    $"{resource.ResourceId.Split('/').Last()} \t {resource.ResourceType} \t {resource.ResourceLocation} \t {resource.ResourceGroupName} \t {resource.CostUSD:N2} USD");

            }
            else
            {
                Console.WriteLine(
                    $"{resource.ResourceId.Split('/').Last()} \t {resource.ResourceType} \t {resource.ResourceLocation} \t {resource.ResourceGroupName} \t {resource.Cost:N2} {resource.Currency}");
            }

            if (settings.ExcludeMeterDetails == false)
            {
                foreach (var metered in resources
                             .Where(a => a.ResourceId == resource.ResourceId)
                             .OrderByDescending(a => a.Cost))
                {
                    Console.WriteLine(
                        $"  + {metered.ServiceName} \t {metered.ServiceTier} \t {metered.Meter} \t {(settings.UseUSD ? metered.CostUSD : metered.Cost):N2} {metered.Currency}");
                }
            }

        }

        if (settings.Top > 0 && totalCount > 0 && settings.Top < totalCount)
        {
            var displayedCount = System.Math.Min(settings.Top, totalCount);
            var costDisplay = settings.UseUSD ? $"{totalCost:N2} USD" : $"{totalCost:N2} {currency}";
            Console.WriteLine();
            Console.WriteLine($"Showing top {displayedCount} of {totalCount} resources (total cost: {costDisplay})");
        }
      
        return Task.CompletedTask;
    }

    public override Task WriteBudgets(BudgetsSettings settings, IEnumerable<BudgetItem> budgets)
    {
        if (settings.SkipHeader == false)
        {
            Console.WriteLine(
                $"Azure Budgets for {settings.Subscription}");

            Console.WriteLine();
        }

        foreach (var budget in budgets.OrderByDescending(a=>a.Name))
        {
            Console.WriteLine(
                $"Budget `{budget.Name}` with an amount of {budget.Amount:N2} (time grain of {budget.TimeGrain} from {budget.StartDate} to {budget.EndDate}) ");

            // Spend tracking
            var status = BudgetStatusHelper.GetStatus(budget);
            Console.WriteLine($"  Status: {status}");

            if (budget.CurrentSpendAmount.HasValue)
            {
                var spendPct = budget.Amount > 0 ? budget.CurrentSpendAmount.Value / budget.Amount * 100 : 0;
                Console.WriteLine($"  Current Spend: {budget.CurrentSpendAmount.Value:N2} {budget.CurrentSpendCurrency} ({spendPct:N1}% of budget)");
            }
            else
            {
                Console.WriteLine("  Current Spend: N/A");
            }

            if (budget.ForecastAmount.HasValue)
            {
                var forecastPct = budget.Amount > 0 ? budget.ForecastAmount.Value / budget.Amount * 100 : 0;
                Console.WriteLine($"  Forecast: {budget.ForecastAmount.Value:N2} {budget.ForecastCurrency} ({forecastPct:N1}% of budget)");
            }
            else
            {
                Console.WriteLine("  Forecast: N/A");
            }

            var remaining = budget.CurrentSpendAmount.HasValue ? budget.Amount - budget.CurrentSpendAmount.Value : budget.Amount;
            Console.WriteLine($"  Remaining: {remaining:N2}");

            foreach (var notification in budget.Notifications)
            {
                Console.WriteLine(
                    $"  {notification.Name} (is {(notification.Enabled?"enabled":"disabled")}) when {notification.Operator} {notification.Threshold:N2} then contact:");
                foreach (var email in notification.ContactEmails)
                {
                    Console.WriteLine($"   - {email}");
                }
                
                foreach (var role in notification.ContactRoles)
                {
                    Console.WriteLine($"   - {role}");
                }

                foreach (var group in notification.ContactGroups)
                {
                    Console.WriteLine($"   - {group}");
                }
            }

            Console.WriteLine();
        }
      
        return Task.CompletedTask;
    }

    public override Task WriteDailyCost(DailyCostSettings settings, IEnumerable<CostDailyItem> dailyCosts)
    {

// Calculate the maximum daily cost
        var maxDailyCost = dailyCosts.GroupBy(a => a.Date)
            .Max(group => group.Sum(item => settings.UseUSD ? item.CostUsd : item.Cost));

        var currency = settings.UseUSD ? "USD" : dailyCosts.First().Currency; 

        Console.WriteLine($"Daily Costs:\n------------");
        Console.WriteLine($"Date        Cost ({currency}) Breakdown");

        foreach (var day in dailyCosts.GroupBy(a => a.Date).OrderBy(a => a.Key))
        {
            var topCosts = day.OrderByDescending(item => settings.UseUSD ? item.CostUsd : item.Cost)
                .Take(settings.OthersCutoff).ToList();

            var othersCost = day.Except(topCosts)
                .Sum(item => settings.UseUSD ? item.CostUsd : item.Cost);

            topCosts.Add(new CostDailyItem(day.Key, "Other", othersCost, othersCost, day.First().Currency, null));

            Console.Write($"{day.Key.ToString(CultureInfo.CurrentCulture)}  ");

            var dailyCost = 0D; // Keep track of the total cost for this day
            var breakdown = new List<string>();

            foreach (var item in topCosts)
            {
                var itemCost = settings.UseUSD ? item.CostUsd : item.Cost;
                dailyCost += itemCost;
                var percentage = (itemCost / day.Sum(i => settings.UseUSD ? i.CostUsd : i.Cost)) * 100;
                breakdown.Add($"{item.Name}: {itemCost.ToString("F2")} ({percentage.ToString("F2")}%)");
            }

            Console.Write($"{dailyCost.ToString("F2")} ");
            Console.WriteLine(string.Join(", ", breakdown));
        }

        return Task.CompletedTask;
    }

    public override Task WriteAnomalyDetectionResults(DetectAnomalySettings settings, List<AnomalyDetectionResult> anomalies)
    {
        if (settings.SkipHeader == false)
        {
            Console.WriteLine("Anomaly Detection Results:");
            Console.WriteLine("--------------------------");
            Console.WriteLine();
        }

        if (anomalies.Count == 0)
        {
            Console.WriteLine("No anomalies detected.");
            return Task.CompletedTask;
        }

        foreach (var dimension in anomalies.GroupBy(a=>a.Name))
        {
            Console.WriteLine($"+ {settings.Dimension}: {dimension.Key}");
            Console.WriteLine();
            foreach (var anomaly in dimension)
            {
                Console.WriteLine($"  - {anomaly.AnomalyType}: {anomaly.Message}");
            }

            Console.WriteLine();
        }
        
        return Task.CompletedTask;
    }

    public override Task WriteAccumulatedDiffCost(DiffSettings settings, AccumulatedCostDetails accumulatedCostSource,
        AccumulatedCostDetails accumulatedCostTarget)
    {
        var culture = CultureInfo.CurrentCulture;

        string GetPreferredCurrency()
        {
            var currencySources = new[]
            {
                accumulatedCostSource.Costs.Select(a => a.Currency),
                accumulatedCostTarget.Costs.Select(a => a.Currency),
                accumulatedCostSource.ByServiceNameCosts.Select(a => a.Currency),
                accumulatedCostTarget.ByServiceNameCosts.Select(a => a.Currency),
                accumulatedCostSource.ByLocationCosts.Select(a => a.Currency),
                accumulatedCostTarget.ByLocationCosts.Select(a => a.Currency),
                accumulatedCostSource.ByResourceGroupCosts.Select(a => a.Currency),
                accumulatedCostTarget.ByResourceGroupCosts.Select(a => a.Currency)
            };

            foreach (var currencySource in currencySources)
            {
                foreach (var candidateCurrency in currencySource)
                {
                    if (!string.IsNullOrWhiteSpace(candidateCurrency))
                    {
                        return candidateCurrency;
                    }
                }
            }

            return "USD";
        }

        var currency = settings.UseUSD ? "USD" : GetPreferredCurrency();

        var sourceRange = accumulatedCostSource.Costs.Any()
            ? $"{accumulatedCostSource.Costs.Min(a => a.Date)} to {accumulatedCostSource.Costs.Max(a => a.Date)}"
            : "N/A";
        var targetRange = accumulatedCostTarget.Costs.Any()
            ? $"{accumulatedCostTarget.Costs.Min(a => a.Date)} to {accumulatedCostTarget.Costs.Max(a => a.Date)}"
            : "N/A";

        Console.WriteLine("Azure Cost Diff");
        Console.WriteLine();
        Console.WriteLine($"Source: {sourceRange}");
        Console.WriteLine($"Target: {targetRange}");

        Console.WriteLine();
        Console.WriteLine("By Service Name:");
        WriteComparisonSection(accumulatedCostSource.ByServiceNameCosts.ToList(),
            accumulatedCostTarget.ByServiceNameCosts.ToList(), settings.UseUSD, currency, culture);

        Console.WriteLine();
        Console.WriteLine("By Location:");
        WriteComparisonSection(accumulatedCostSource.ByLocationCosts.ToList(),
            accumulatedCostTarget.ByLocationCosts.ToList(), settings.UseUSD, currency, culture);

        Console.WriteLine();
        Console.WriteLine("By Resource Group:");
        WriteComparisonSection(accumulatedCostSource.ByResourceGroupCosts.ToList(),
            accumulatedCostTarget.ByResourceGroupCosts.ToList(), settings.UseUSD, currency, culture);

        var totalSource = accumulatedCostSource.Costs.Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
        var totalTarget = accumulatedCostTarget.Costs.Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
        var totalDiff = totalTarget - totalSource;
        var totalDiffSign = totalDiff >= 0 ? "+" : "";

        Console.WriteLine();
        Console.WriteLine("Summary:");
        Console.WriteLine($"  Source Total: {totalSource.ToString("N2", culture)} {currency}");
        Console.WriteLine($"  Target Total: {totalTarget.ToString("N2", culture)} {currency}");
        Console.WriteLine($"  Change: {totalDiffSign}{totalDiff.ToString("N2", culture)} {currency}");

        return Task.CompletedTask;
    }

    private static void WriteComparisonSection(
        List<CostNamedItem> sourceItems,
        List<CostNamedItem> targetItems,
        bool useUSD,
        string currency,
        CultureInfo culture)
    {
        var sourceCosts = sourceItems
            .GroupBy(a => a.ItemName)
            .ToDictionary(g => g.Key, g => g.Sum(a => useUSD ? a.CostUsd : a.Cost));

        var targetCosts = targetItems
            .GroupBy(a => a.ItemName)
            .ToDictionary(g => g.Key, g => g.Sum(a => useUSD ? a.CostUsd : a.Cost));

        var allItems = sourceCosts.Keys
            .Union(targetCosts.Keys)
            .OrderByDescending(name =>
                Math.Max(
                    sourceCosts.GetValueOrDefault(name),
                    targetCosts.GetValueOrDefault(name)))
            .ToList();

        foreach (var item in allItems)
        {
            sourceCosts.TryGetValue(item, out var sourceCost);
            targetCosts.TryGetValue(item, out var targetCost);
            var diff = targetCost - sourceCost;
            var diffSign = diff >= 0 ? "+" : "";
            Console.WriteLine($"  {item}: {sourceCost.ToString("N2", culture)} -> {targetCost.ToString("N2", culture)} ({diffSign}{diff.ToString("N2", culture)}) {currency}");
        }
    }
    
    public override Task WriteRegions(RegionsSettings settings, IReadOnlyCollection<AzureRegion> regions)
    {
        Console.WriteLine("Azure Regions");
        Console.WriteLine();

        foreach (var region in regions.OrderBy(a => a.continent).ThenBy(a => a.geographyId))
        {
            Console.WriteLine($"  {region.id} ({region.displayName}) - {region.location}");
        }

        return Task.CompletedTask;
    }

    public override Task WriteCostByTag(CostByTagSettings settings, Dictionary<string, Dictionary<string, List<CostResourceItem>>> byTags)
    {
        foreach (var (tagKey, tagValues) in byTags)
        {
            Console.WriteLine($"Tag: {tagKey}");
            Console.WriteLine();

            foreach (var (tagValue, resources) in tagValues.OrderByDescending(kv => kv.Value.Sum(r => r.Cost)))
            {
                var totalCost = resources.Sum(r => settings.UseUSD ? r.CostUSD : r.Cost);
                var currency = resources.FirstOrDefault()?.Currency ?? "USD";
                Console.WriteLine($"  {tagValue}: {totalCost:N2} {currency} ({resources.Count} resources)");
            }

            Console.WriteLine();
        }

        return Task.CompletedTask;
    }

    public override Task WritePricesPerRegion(WhatIfSettings settings, Dictionary<UsageDetails, List<PriceRecord>> pricesByRegion)
    {
        foreach (var (resource, prices) in pricesByRegion)
        {
            Console.WriteLine($"Resource: {resource.name} ({resource.properties?.meterDetails?.meterName})");

            foreach (var price in prices.OrderBy(p => p.RetailPrice))
            {
                Console.WriteLine($"  {price.ArmRegionName}: {price.RetailPrice:N4} {price.CurrencyCode}/{price.UnitOfMeasure}");
            }

            Console.WriteLine();
        }

        return Task.CompletedTask;
    }

    public override Task WriteDevTestComparison(WhatIfSettings settings, IEnumerable<DevTestComparisonItem> items)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            Console.WriteLine("No Virtual Machine resources found for DevTest comparison.");
            return Task.CompletedTask;
        }

        Console.WriteLine("DevTest Pricing Comparison");
        Console.WriteLine(new string('=', 60));

        foreach (var item in itemList.OrderByDescending(i => i.Savings ?? 0))
        {
            Console.WriteLine($"Resource: {item.ResourceName} ({item.ResourceGroup})");
            Console.WriteLine($"  Meter: {item.MeterName} | Region: {item.Region}");
            Console.WriteLine($"  Quantity: {item.Quantity:N2} {item.UnitOfMeasure}");
            Console.WriteLine($"  Current Cost: {item.CurrentCost:N2} {item.Currency}");
            Console.WriteLine($"  DevTest Cost: {(item.DevTestCost.HasValue ? $"{item.DevTestCost:N2} {item.Currency}" : "N/A")}");
            Console.WriteLine($"  Savings: {(item.Savings.HasValue ? $"{item.Savings:N2} {item.Currency} ({item.SavingsPercentage:N1}%)" : "N/A")}");
            Console.WriteLine();
        }

        var totalCurrent = itemList.Sum(i => i.CurrentCost);
        var totalDevTest = itemList.Where(i => i.DevTestCost.HasValue).Sum(i => i.DevTestCost!.Value);
        var totalSavings = totalCurrent - totalDevTest;
        var currency = itemList.First().Currency;
        Console.WriteLine($"Total Current Cost: {totalCurrent:N2} {currency}");
        Console.WriteLine($"Total DevTest Cost: {totalDevTest:N2} {currency}");
        Console.WriteLine($"Total Savings: {totalSavings:N2} {currency} ({(totalCurrent > 0 ? totalSavings / totalCurrent * 100 : 0):N1}%)");

        return Task.CompletedTask;
    }

    public override Task WriteThreshold(ThresholdSettings settings, ThresholdResult result)
    {
        var status = result.IsThresholdExceeded ? "EXCEEDED" : "OK";
        Console.WriteLine($"Threshold Check [{result.SubCommand}]: {status}");
        Console.WriteLine($"  {result.Message}");
        if (result.ActualValue.HasValue)
            Console.WriteLine($"  Actual value: {result.ActualValue.Value:N2}");
        if (result.ThresholdValue.HasValue)
            Console.WriteLine($"  Threshold: {result.ThresholdValue.Value:N2}");
        return Task.CompletedTask;
    }
    
}