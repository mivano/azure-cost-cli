using System.Globalization;
using System.Text;
using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.Commands.Budgets;
using AzureCostCli.Commands.CostByResource;
using AzureCostCli.Commands.CostByTag;
using AzureCostCli.Commands.DailyCost;
using AzureCostCli.Commands.DetectAnomaly;
using AzureCostCli.Commands.Diff;
using AzureCostCli.Commands.Regions;
using AzureCostCli.Commands.WhatIf;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;

namespace AzureCostCli.OutputFormatters;

public class MarkdownOutputFormatter : BaseOutputFormatter
{
    public override Task WriteAccumulatedCost(AccumulatedCostSettings settings,AccumulatedCostDetails accumulatedCostDetails)
    {
        if (accumulatedCostDetails.Costs.Any() == false && accumulatedCostDetails.ForecastedCosts.Any() == false)
        {
            Console.WriteLine("# Azure Cost Overview");
            Console.WriteLine();
            Console.WriteLine("**No data found**");
            return Task.CompletedTask;
        }

        var hasCosts = accumulatedCostDetails.Costs.Any();
        
        var output = new
        {
            costs = new
            {
                todaysCost = accumulatedCostDetails.Costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow)).Sum(a => settings.UseUSD ? a.CostUsd :a.Cost),
                yesterdayCost = accumulatedCostDetails.Costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))
                    .Sum(a =>settings.UseUSD ? a.CostUsd :a.Cost),
                lastSevenDaysCost = accumulatedCostDetails.Costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)))
                    .Sum(a => settings.UseUSD ? a.CostUsd :a.Cost),
                lastThirtyDaysCost = accumulatedCostDetails.Costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)))
                    .Sum(a => settings.UseUSD ? a.CostUsd :a.Cost),
                totalCostInTimeframe = accumulatedCostDetails.Costs.Sum(a => settings.UseUSD ? a.CostUsd : a.Cost)
            },
        };

        var currency = settings.UseUSD ? "USD" : (hasCosts ? accumulatedCostDetails.Costs.FirstOrDefault()?.Currency : accumulatedCostDetails.ForecastedCosts.FirstOrDefault()?.Currency);
        var culture = CultureInfo.GetCultureInfo("en-US");

        Console.WriteLine("# Azure Cost Overview");
        Console.WriteLine();
        
        if (hasCosts)
        {
            Console.WriteLine(
                $"> Accumulated cost for subscription id `{accumulatedCostDetails.Subscription.displayName}` from **{accumulatedCostDetails.Costs.Min(a => a.Date)}** to **{accumulatedCostDetails.Costs.Max(a => a.Date)}**");
            Console.WriteLine();
            Console.WriteLine("## Totals");
            Console.WriteLine();
            Console.WriteLine("|Period|Amount|");
            Console.WriteLine("|---|---:|");
            Console.WriteLine($"|Today|{output.costs.todaysCost:N2} {currency}|");
            Console.WriteLine($"|Yesterday|{output.costs.yesterdayCost:N2} {currency}|");
            Console.WriteLine($"|Last 7 days|{output.costs.lastSevenDaysCost:N2} {currency}|");
            Console.WriteLine($"|Last 30 days|{output.costs.lastThirtyDaysCost:N2} {currency}|");
            Console.WriteLine($"|Total cost in timeframe|{output.costs.totalCostInTimeframe:N2} {currency}|");
        }
        else
        {
            Console.WriteLine(
                $"> Forecasted cost for subscription id `{accumulatedCostDetails.Subscription.displayName}` from **{accumulatedCostDetails.ForecastedCosts.Min(a => a.Date)}** to **{accumulatedCostDetails.ForecastedCosts.Max(a => a.Date)}**");
            Console.WriteLine();
            Console.WriteLine("## Forecasted Costs");
            Console.WriteLine();
            Console.WriteLine("No historical cost data available. Showing forecasted costs only.");
        }

        // Generate a gantt chart using mermaidjs
        Console.WriteLine();
        Console.WriteLine("```mermaid");
        Console.WriteLine("gantt");
        Console.WriteLine("   title Accumulated cost");
        Console.WriteLine("   dateFormat  X");
        Console.WriteLine("   axisFormat %s");

        var accumulatedCost = accumulatedCostDetails.Costs.OrderBy(x => x.Date).ToList();
        double accumulatedCostValue = 0.0;
        foreach (var day in accumulatedCost)
        {
            
            double costValue =settings.UseUSD ? day.CostUsd :day.Cost;
            accumulatedCostValue += costValue;
            
            Console.WriteLine($"   section {day.Date.ToString("dd MMM")}");
            Console.WriteLine($"   {currency} {Math.Round(accumulatedCostValue, 2):F2} :0, {Math.Round(accumulatedCostValue* 100, 0) }");
        }

        var forecastedData = hasCosts 
            ? accumulatedCostDetails.ForecastedCosts.Where(x => x.Date > accumulatedCost.Last().Date).OrderBy(x => x.Date).ToList()
            : accumulatedCostDetails.ForecastedCosts.OrderBy(x => x.Date).ToList();
      
        foreach (var day in forecastedData)
        {
            double costValue = settings.UseUSD ? day.CostUsd :day.Cost;;
            accumulatedCostValue += costValue;
            Console.WriteLine($"   section {day.Date.ToString("dd MMM")}");
            Console.WriteLine($"   {currency} {Math.Round(accumulatedCostValue, 2):F2} : done, 0, {Math.Round(accumulatedCostValue* 100, 0) }");
        }

        Console.WriteLine("```");
        
        Console.WriteLine();
        Console.WriteLine("## By Service Name");
        Console.WriteLine();
        Console.WriteLine("|Service|Amount|");
        Console.WriteLine("|---|---:|");
        foreach (var cost in accumulatedCostDetails.ByServiceNameCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"|{cost.ItemName}|{(settings.UseUSD ? cost.CostUsd :cost.Cost):N2} {currency}|");
        }
        
        // Create a pie chart using mermaidjs
        Console.WriteLine();
        Console.WriteLine("```mermaid");
        Console.WriteLine("pie");
        Console.WriteLine("   title Cost by service");
        foreach (var cost in accumulatedCostDetails.ByServiceNameCosts.TrimList(threshold: settings.OthersCutoff))
        {
            var name = string.IsNullOrWhiteSpace(cost.ItemName) ? "(Unknown)" : cost.ItemName;
            Console.WriteLine($"   \"{name}\" : {(settings.UseUSD ? cost.CostUsd :cost.Cost).ToString("F2", culture)}");
        }
        Console.WriteLine("```");

        Console.WriteLine();
        Console.WriteLine("## By Location");
        Console.WriteLine();
        Console.WriteLine("|Location|Amount|");
        Console.WriteLine("|---|---:|");
        foreach (var cost in accumulatedCostDetails.ByLocationCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"|{cost.ItemName}|{(settings.UseUSD ? cost.CostUsd :cost.Cost):N2} {currency}|");
        }

      

        // Create a pie chart using mermaidjs
        Console.WriteLine();
        Console.WriteLine("```mermaid");
        Console.WriteLine("pie");
        Console.WriteLine("   title Cost by location");
        foreach (var cost in accumulatedCostDetails.ByLocationCosts.TrimList(threshold: settings.OthersCutoff))
        {
            var name = string.IsNullOrWhiteSpace(cost.ItemName) ? "(Unknown)" : cost.ItemName;
            Console.WriteLine($"   \"{name}\" : {(settings.UseUSD ? cost.CostUsd :cost.Cost).ToString("F2", culture)}");
        }
        Console.WriteLine("```");

        if (accumulatedCostDetails.BySubscriptionCosts!=null &&settings.GetScope.Name.Equals("EnrollmentAccount", StringComparison.InvariantCultureIgnoreCase))
        {
            Console.WriteLine();
            Console.WriteLine("## By Subscriptions");
            Console.WriteLine();
            Console.WriteLine("|Resource Group|Amount|");
            Console.WriteLine("|---|---:|");
            foreach (var cost in accumulatedCostDetails.BySubscriptionCosts.TrimList(threshold: settings.OthersCutoff))
            {
                Console.WriteLine($"|{cost.ItemName}|{(settings.UseUSD ? cost.CostUsd :cost.Cost):N2} {currency}|");
            }

            // Generate a pie chart using mermaidjs
            Console.WriteLine();
            Console.WriteLine("```mermaid");
            Console.WriteLine("pie");
            Console.WriteLine("   title Cost by Subscription");
            foreach (var cost in accumulatedCostDetails.BySubscriptionCosts.TrimList(threshold: settings.OthersCutoff))
            {
                var name = string.IsNullOrWhiteSpace(cost.ItemName) ? "(Unknown)" : cost.ItemName;
                Console.WriteLine($"   \"{name}\" : {(settings.UseUSD ? cost.CostUsd :cost.Cost).ToString("F2", culture)}");
            }
            Console.WriteLine("```");
        }

        if (settings.GetScope.IsSubscriptionBased)
        {
            Console.WriteLine();
            Console.WriteLine("## By Resource Group");
            Console.WriteLine();
            Console.WriteLine("|Resource Group|Amount|");
            Console.WriteLine("|---|---:|");
            foreach (var cost in accumulatedCostDetails.ByResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
            {
                Console.WriteLine($"|{cost.ItemName}|{(settings.UseUSD ? cost.CostUsd : cost.Cost):N2} {currency}|");
            }

            // Generate a pie chart using mermaidjs
            Console.WriteLine();
            Console.WriteLine("```mermaid");
            Console.WriteLine("pie");
            Console.WriteLine("   title Cost by resource group");
            foreach (var cost in accumulatedCostDetails.ByResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
            {
                var name = string.IsNullOrWhiteSpace(cost.ItemName) ? "(Unknown)" : cost.ItemName;
                Console.WriteLine(
                    $"   \"{name}\" : {(settings.UseUSD ? cost.CostUsd : cost.Cost).ToString("F2", culture)}");
            }

            Console.WriteLine("```");
        }

        Console.WriteLine();
        Console.WriteLine($"<sup>Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} for subscription with id `{accumulatedCostDetails.Subscription.subscriptionId}`</sup>");

        return Task.CompletedTask;
    }

    public override Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources,
        int totalCount = 0, double totalCost = 0, string currency = "USD")
    {

        if (settings.ExcludeMeterDetails)
        {
            if (settings.SkipHeader == false)
            {
                Console.WriteLine("# Azure Cost by Resource");
                Console.WriteLine();
                Console.WriteLine(
                    "| ResourceName | ResourceType | Location | ResourceGroupName | Amount |");
                Console.WriteLine("|---|---|---|---|---|---|---|---:|");
            }

            foreach (var cost in resources)
            {
                Console.WriteLine(
                    $"|{cost.ResourceId.Split('/').Last()} | {cost.ResourceType} | {cost.ResourceLocation} | {cost.ResourceGroupName} | {(settings.UseUSD ? cost.CostUSD : cost.Cost):N2} {(settings.UseUSD ? "USD" : cost.Currency)} |");
            }
        }
        else
        {

            if (settings.SkipHeader == false)
            {
                Console.WriteLine("# Azure Cost by Resource");
                Console.WriteLine();
                Console.WriteLine(
                    "| ResourceName | ResourceType | Location | ResourceGroupName | ServiceName | ServiceTier | Meter | Amount |");
                Console.WriteLine("|---|---|---|---|---|---|---|---:|");
            }

            foreach (var cost in resources)
            {
                Console.WriteLine(
                    $"|{cost.ResourceId.Split('/').Last()} | {cost.ResourceType} | {cost.ResourceLocation} | {cost.ResourceGroupName} |  {cost.ServiceName} | {cost.ServiceTier} | {cost.Meter} | {(settings.UseUSD ? cost.CostUSD : cost.Cost):N2} {(settings.UseUSD ? "USD" : cost.Currency)} |");
            }
        }

        if (settings.Top > 0 && totalCount > 0 && settings.Top < totalCount)
        {
            var displayedCount = Math.Min(settings.Top, totalCount);
            var costDisplay = settings.UseUSD ? $"{totalCost:N2} USD" : $"{totalCost:N2} {currency}";
            Console.WriteLine();
            Console.WriteLine($"> Showing top {displayedCount} of {totalCount} resources (total cost: {costDisplay})");
        }

        return Task.CompletedTask;
    }

    public override Task WriteBudgets(BudgetsSettings settings, IEnumerable<BudgetItem> budgets)
    {
        if (settings.SkipHeader == false)
        {
            Console.WriteLine(
                $"# Azure Budgets for {settings.Subscription}");

            Console.WriteLine();
        }

        // Summary table
        Console.WriteLine("| Budget | Amount | Status | Current Spend | Forecast | Remaining |");
        Console.WriteLine("|--------|--------|--------|---------------|----------|-----------|");

        foreach (var budget in budgets.OrderByDescending(a=>a.Name))
        {
            var status = BudgetStatusHelper.GetStatus(budget);
            var statusEmoji = status switch
            {
                "EXCEEDED" => "🔴 EXCEEDED",
                "AT-RISK" => "🟡 AT-RISK",
                _ => "🟢 OK"
            };

            var spendText = budget.CurrentSpendAmount.HasValue
                ? $"{budget.CurrentSpendAmount.Value:N2} {budget.CurrentSpendCurrency} ({(budget.Amount > 0 ? budget.CurrentSpendAmount.Value / budget.Amount * 100 : 0):N1}%)"
                : "N/A";

            var forecastText = budget.ForecastAmount.HasValue
                ? $"{budget.ForecastAmount.Value:N2} {budget.ForecastCurrency} ({(budget.Amount > 0 ? budget.ForecastAmount.Value / budget.Amount * 100 : 0):N1}%)"
                : "N/A";

            var remaining = budget.CurrentSpendAmount.HasValue ? budget.Amount - budget.CurrentSpendAmount.Value : budget.Amount;

            var escapedName = budget.Name.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
            Console.WriteLine($"| {escapedName} | {budget.Amount:N2} | {statusEmoji} | {spendText} | {forecastText} | {remaining:N2} |");
        }

        Console.WriteLine();

        foreach (var budget in budgets.OrderByDescending(a=>a.Name))
        {
            Console.WriteLine(
                $"## Budget `{budget.Name}` ");
            Console.WriteLine($"Has an amount of {budget.Amount:N2}");
            Console.WriteLine($"The time grain is {budget.TimeGrain} and the time period is {budget.StartDate} to {budget.EndDate}");

            Console.WriteLine();
            
            foreach (var notification in budget.Notifications)
            {
                Console.WriteLine(
                    $"### Notification `{notification.Name}`");
                Console.WriteLine($"This notification is {(notification.Enabled?"enabled":"disabled")} and when {notification.Operator} {notification.Threshold:N2} then contact:");
                foreach (var email in notification.ContactEmails)
                {
                    Console.WriteLine($" - {email}");
                }
                
                foreach (var role in notification.ContactRoles)
                {
                    Console.WriteLine($" - {role}");
                }

                foreach (var group in notification.ContactGroups)
                {
                    Console.WriteLine($" - {group}");
                }
            }

            Console.WriteLine();
        }

        if (settings.SkipHeader == false)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"<sup>Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} for subscription with id `{settings.Subscription}`</sup>");
        }

        return Task.CompletedTask;
    }

    public override Task WriteDailyCost(DailyCostSettings settings, IEnumerable<CostDailyItem> dailyCosts)
    {
       
        var currency = settings.UseUSD ? "USD" : dailyCosts.First().Currency;

        var markdown = new StringBuilder();

        markdown.AppendLine("# Daily Costs\n");

// Markdown table header
        markdown.AppendLine($"| Date | Cost ({currency}) | Breakdown |");
        markdown.AppendLine("|------|----------------|-----------|");

        foreach (var day in dailyCosts.GroupBy(a => a.Date).OrderBy(a => a.Key))
        {
            var topCosts = day.OrderByDescending(item => settings.UseUSD ? item.CostUsd : item.Cost)
                .Take(settings.OthersCutoff).ToList();

            var othersCost = day.Except(topCosts)
                .Sum(item => settings.UseUSD ? item.CostUsd : item.Cost);

            topCosts.Add(new CostDailyItem(day.Key, "Other", othersCost, othersCost, day.First().Currency, null));

            var dailyCost = 0D; // Keep track of the total cost for this day
            var breakdown = new List<string>();

            foreach (var item in topCosts)
            {
                var itemCost = settings.UseUSD ? item.CostUsd : item.Cost;
                dailyCost += itemCost;
                var percentage = (itemCost / day.Sum(i => settings.UseUSD ? i.CostUsd : i.Cost)) * 100;
                breakdown.Add($"**{item.Name}**: `{itemCost.ToString("F2")}` (_{percentage.ToString("F2")}%_)");
            }

            // Markdown table row
            markdown.AppendLine($"| **{day.Key.ToString("yyyy-MM-dd")}** | **{dailyCost.ToString("F2")}** | {string.Join(", ", breakdown)} |");
        }

// Output markdown
        Console.WriteLine(markdown.ToString());

        if (settings.SkipHeader == false)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"<sup>Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} for subscription with id `{settings.Subscription}`</sup>");
        }

        return Task.CompletedTask;
    }

    public override Task WriteAnomalyDetectionResults(DetectAnomalySettings settings, List<AnomalyDetectionResult> anomalies)
    {
        if (settings.SkipHeader == false)
        {
            Console.WriteLine("# Anomaly Detection Results");
            Console.WriteLine();
        }

        if (anomalies.Count == 0)
        {
            Console.WriteLine("No anomalies detected.");
            return Task.CompletedTask;
        }

        foreach (var dimension in anomalies.GroupBy(a=>a.Name))
        {
            Console.WriteLine($"## {settings.Dimension}: {dimension.Key}");
            Console.WriteLine();
            Console.WriteLine("| Anomaly Type | Message |");
            Console.WriteLine("|---|---|");
            foreach (var anomaly in dimension)
            {
                Console.WriteLine($"|{anomaly.AnomalyType}| {anomaly.Message}|");
            }

            Console.WriteLine();
        }

        if (settings.SkipHeader == false)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"<sup>Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} for subscription with id `{settings.Subscription}`</sup>");
        }

        return Task.CompletedTask;
    }

    public override Task WriteAccumulatedDiffCost(DiffSettings settings, AccumulatedCostDetails accumulatedCostSource,
        AccumulatedCostDetails accumulatedCostTarget)
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        var currency = "USD";
        if (!settings.UseUSD)
        {
            var firstCost = accumulatedCostSource.Costs.FirstOrDefault();
            if (firstCost != null && !string.IsNullOrEmpty(firstCost.Currency))
            {
                currency = firstCost.Currency;
            }
        }
        
        // Create header info
        var sourceRange = accumulatedCostSource.Costs.Any()
            ? $"{accumulatedCostSource.Costs.Min(a => a.Date)} to {accumulatedCostSource.Costs.Max(a => a.Date)}"
            : "N/A";
        var targetRange = accumulatedCostTarget.Costs.Any()
            ? $"{accumulatedCostTarget.Costs.Min(a => a.Date)} to {accumulatedCostTarget.Costs.Max(a => a.Date)}"
            : "N/A";
        
        Console.WriteLine("# Azure Cost Diff");
        Console.WriteLine();
        Console.WriteLine($"**Source**: {sourceRange}");
        Console.WriteLine();
        Console.WriteLine($"**Target**: {targetRange}");
        Console.WriteLine();
        
        // Compare costs by service names
        Console.WriteLine("## By Service Name");
        Console.WriteLine();
        WriteComparisonTable(
            accumulatedCostSource.ByServiceNameCosts.ToList(),
            accumulatedCostTarget.ByServiceNameCosts.ToList(),
            settings.UseUSD,
            currency,
            culture);
        
        // Compare costs by location
        Console.WriteLine();
        Console.WriteLine("## By Location");
        Console.WriteLine();
        WriteComparisonTable(
            accumulatedCostSource.ByLocationCosts.ToList(),
            accumulatedCostTarget.ByLocationCosts.ToList(),
            settings.UseUSD,
            currency,
            culture);
        
        // Compare costs by resource group
        Console.WriteLine();
        Console.WriteLine("## By Resource Group");
        Console.WriteLine();
        WriteComparisonTable(
            accumulatedCostSource.ByResourceGroupCosts.ToList(),
            accumulatedCostTarget.ByResourceGroupCosts.ToList(),
            settings.UseUSD,
            currency,
            culture);
        
        // Calculate totals
        var totalSource = accumulatedCostSource.Costs.Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
        var totalTarget = accumulatedCostTarget.Costs.Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
        var totalDiff = totalTarget - totalSource;
        
        // Summary section
        Console.WriteLine();
        Console.WriteLine("## Summary");
        Console.WriteLine();
        Console.WriteLine("|Comparison|Source|Target|Change|");
        Console.WriteLine("|---|---:|---:|---:|");
        
        var diffSign = totalDiff >= 0 ? "+" : "";
        Console.WriteLine($"|**TOTAL COSTS**|{totalSource.ToString("N2", culture)} {currency}|{totalTarget.ToString("N2", culture)} {currency}|{diffSign}{totalDiff.ToString("N2", culture)} {currency}|");
        
        return Task.CompletedTask;
    }
    
    private void WriteComparisonTable(
        List<CostNamedItem> sourceItems,
        List<CostNamedItem> targetItems,
        bool useUSD,
        string currency,
        CultureInfo culture)
    {
        var allItems = sourceItems.Select(a => a.ItemName)
            .Union(targetItems.Select(a => a.ItemName))
            .OrderByDescending(name =>
                Math.Max(
                    sourceItems.Where(a => a.ItemName == name).Sum(a => useUSD ? a.CostUsd : a.Cost),
                    targetItems.Where(a => a.ItemName == name).Sum(a => useUSD ? a.CostUsd : a.Cost)
                ))
            .ToList();
        
        Console.WriteLine("|Name|Source|Target|Change|");
        Console.WriteLine("|---|---:|---:|---:|");
        
        var totalSource = 0.0;
        var totalTarget = 0.0;
        
        foreach (var item in allItems)
        {
            var sourceCost = sourceItems
                .Where(a => a.ItemName == item)
                .Sum(a => useUSD ? a.CostUsd : a.Cost);
            
            var targetCost = targetItems
                .Where(a => a.ItemName == item)
                .Sum(a => useUSD ? a.CostUsd : a.Cost);
            
            totalSource += sourceCost;
            totalTarget += targetCost;
            
            var diff = targetCost - sourceCost;
            var diffSign = diff >= 0 ? "+" : "";
            
            Console.WriteLine($"|{item}|{sourceCost.ToString("N2", culture)} {currency}|{targetCost.ToString("N2", culture)} {currency}|{diffSign}{diff.ToString("N2", culture)} {currency}|");
        }
        
        // Add subtotal row
        var totalDiff = totalTarget - totalSource;
        var totalDiffSign = totalDiff >= 0 ? "+" : "";
        Console.WriteLine($"|**SUBTOTAL**|**{totalSource.ToString("N2", culture)} {currency}**|**{totalTarget.ToString("N2", culture)} {currency}**|**{totalDiffSign}{totalDiff.ToString("N2", culture)} {currency}**|");
    }
    
    public override Task WriteRegions(RegionsSettings settings, IReadOnlyCollection<AzureRegion> regions)
    {
        Console.WriteLine("# Azure Regions");
        Console.WriteLine();
        Console.WriteLine("|Region|Geography|Display Name|Location|");
        Console.WriteLine("|---|---|---|---|");

        foreach (var region in regions.OrderBy(a => a.continent).ThenBy(a => a.geographyId))
        {
            Console.WriteLine($"|{region.continent}|{region.geographyId}|{region.displayName}|{region.location}|");
        }

        return Task.CompletedTask;
    }

    public override Task WriteCostByTag(CostByTagSettings settings, Dictionary<string, Dictionary<string, List<CostResourceItem>>> byTags)
    {
        foreach (var (tagKey, tagValues) in byTags)
        {
            Console.WriteLine($"## Tag: {tagKey}");
            Console.WriteLine();
            Console.WriteLine("|Tag Value|Cost|Resources|");
            Console.WriteLine("|---|---:|---:|");

            foreach (var (tagValue, resources) in tagValues.OrderByDescending(kv => kv.Value.Sum(r => r.Cost)))
            {
                var totalCost = resources.Sum(r => settings.UseUSD ? r.CostUSD : r.Cost);
                var currency = resources.FirstOrDefault()?.Currency ?? "USD";
                Console.WriteLine($"|{tagValue}|{totalCost:N2} {currency}|{resources.Count}|");
            }

            Console.WriteLine();
        }

        return Task.CompletedTask;
    }

    public override Task WritePricesPerRegion(WhatIfSettings settings, Dictionary<UsageDetails, List<PriceRecord>> pricesByRegion)
    {
        foreach (var (resource, prices) in pricesByRegion)
        {
            Console.WriteLine($"## {resource.name} ({resource.properties?.meterDetails?.meterName})");
            Console.WriteLine();
            Console.WriteLine("|Region|Retail Price|Unit|");
            Console.WriteLine("|---|---:|---|");

            foreach (var price in prices.OrderBy(p => p.RetailPrice))
            {
                Console.WriteLine($"|{price.ArmRegionName}|{price.RetailPrice:N4} {price.CurrencyCode}|{price.UnitOfMeasure}|");
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

        Console.WriteLine("# DevTest Pricing Comparison");
        Console.WriteLine();
        Console.WriteLine("|Resource|Resource Group|Meter|Region|Quantity|Current Cost|DevTest Cost|Savings|Savings %|");
        Console.WriteLine("|---|---|---|---|---:|---:|---:|---:|---:|");

        foreach (var item in itemList.OrderByDescending(i => i.Savings ?? 0))
        {
            var devTestCost = item.DevTestCost.HasValue ? $"{item.DevTestCost:N2} {item.Currency}" : "N/A";
            var savings = item.Savings.HasValue ? $"{item.Savings:N2} {item.Currency}" : "N/A";
            var savingsPct = item.SavingsPercentage.HasValue ? $"{item.SavingsPercentage:N1}%" : "N/A";

            Console.WriteLine($"|{item.ResourceName}|{item.ResourceGroup}|{item.MeterName}|{item.Region}|{item.Quantity:N2}|{item.CurrentCost:N2} {item.Currency}|{devTestCost}|{savings}|{savingsPct}|");
        }

        var totalCurrent = itemList.Sum(i => i.CurrentCost);
        var totalDevTest = itemList.Where(i => i.DevTestCost.HasValue).Sum(i => i.DevTestCost!.Value);
        var totalSavings = totalCurrent - totalDevTest;
        var currency = itemList.First().Currency;

        Console.WriteLine();
        Console.WriteLine($"**Total Current Cost:** {totalCurrent:N2} {currency}  ");
        Console.WriteLine($"**Total DevTest Cost:** {totalDevTest:N2} {currency}  ");
        Console.WriteLine($"**Total Savings:** {totalSavings:N2} {currency} ({(totalCurrent > 0 ? totalSavings / totalCurrent * 100 : 0):N1}%)");

        return Task.CompletedTask;
    }
}