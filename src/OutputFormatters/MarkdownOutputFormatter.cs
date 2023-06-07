using System.Globalization;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public class MarkdownOutputFormatter : BaseOutputFormatter
{
    public override Task WriteAccumulatedCost(AccumulatedCostSettings settings,AccumulatedCostDetails accumulatedCostDetails)
    {
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
            },
        };

        var currency = settings.UseUSD ? "USD":accumulatedCostDetails.Costs.FirstOrDefault()?.Currency;
        var culture = CultureInfo.GetCultureInfo("en-US");

        Console.WriteLine("# Azure Cost Overview");
        Console.WriteLine();
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

        var forecastedData = accumulatedCostDetails.ForecastedCosts.Where(x => x.Date > accumulatedCost.Last().Date).OrderBy(x => x.Date)
            .ToList();
      
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

        Console.WriteLine();
        Console.WriteLine("## By Resource Group");
        Console.WriteLine();
        Console.WriteLine("|Resource Group|Amount|");
        Console.WriteLine("|---|---:|");
        foreach (var cost in accumulatedCostDetails.ByResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"|{cost.ItemName}|{(settings.UseUSD ? cost.CostUsd :cost.Cost):N2} {currency}|");
        }

        // Generate a pie chart using mermaidjs
        Console.WriteLine();
        Console.WriteLine("```mermaid");
        Console.WriteLine("pie");
        Console.WriteLine("   title Cost by resource group");
        foreach (var cost in accumulatedCostDetails.ByResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
        {
            var name = string.IsNullOrWhiteSpace(cost.ItemName) ? "(Unknown)" : cost.ItemName;
            Console.WriteLine($"   \"{name}\" : {(settings.UseUSD ? cost.CostUsd :cost.Cost).ToString("F2", culture)}");
        }
        Console.WriteLine("```");
        
        Console.WriteLine();
        Console.WriteLine($"<sup>Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} for subscription with id `{accumulatedCostDetails.Subscription.subscriptionId}`</sup>");

        return Task.CompletedTask;
    }

    public override Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources)
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
            Console.WriteLine($"|{cost.ResourceId.Split('/').Last()} | {cost.ResourceType} | {cost.ResourceLocation} | {cost.ResourceGroupName} |  {cost.ServiceName} | {cost.ServiceTier} | {cost.Meter} | {(settings.UseUSD ? cost.CostUSD :cost.Cost):N2} {(settings.UseUSD ? "USD" :cost.Currency)} |");
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
      
        return Task.CompletedTask;
    }
}