using System.Globalization;
using AzureCostCli.Commands.Regions;
using AzureCostCli.Commands.WhatIf;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public class TextOutputFormatter : BaseOutputFormatter
{
    public override Task WriteAccumulatedCost(AccumulatedCostSettings settings, AccumulatedCostDetails accumulatedCostDetails)
    {
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

        Console.WriteLine();
        Console.WriteLine("By Resource Group:");
        foreach (var cost in accumulatedCostDetails.ByResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"  {cost.ItemName}: {(settings.UseUSD ? cost.CostUsd :  cost.Cost):N2} {currency}");
        }
        
        return Task.CompletedTask;
    }

    public override Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources)
    {
        if (settings.SkipHeader == false)
        {
            Console.WriteLine(
                $"Azure Cost Overview for {settings.Subscription} by resource");

            Console.WriteLine();
        }

        foreach (var resource in resources.OrderByDescending(a=>a.Cost))
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

            topCosts.Add(new CostDailyItem(day.Key, "Other", othersCost, othersCost, day.First().Currency));

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

    public override Task WriteRegions(RegionsSettings settings, IReadOnlyCollection<AzureRegion> regions)
    {
        throw new NotImplementedException();
    }

    public override Task WriteCostByTag(CostByTagSettings settings, Dictionary<string, Dictionary<string, List<CostResourceItem>>> byTags)
    {
        throw new NotImplementedException();
    }

    public override Task WritePricesPerRegion(WhatIfSettings settings, Dictionary<UsageDetails, List<PriceRecord>> pricesByRegion)
    {
        throw new NotImplementedException();
    }
    
}