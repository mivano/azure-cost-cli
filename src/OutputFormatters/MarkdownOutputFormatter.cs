using System.Globalization;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public class MarkdownOutputFormatter : BaseOutputFormatter
{
    public override Task WriteAccumulatedCost(AccumulatedCostSettings settings,
        IEnumerable<CostItem> costs,
        IEnumerable<CostItem> forecastedCosts,
        IEnumerable<CostNamedItem> byServiceNameCosts,
        IEnumerable<CostNamedItem> byLocationCosts,
        IEnumerable<CostNamedItem> byResourceGroupCosts)
    {
        var output = new
        {
            costs = new
            {
                todaysCost = costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow)).Sum(a => a.Cost),
                yesterdayCost = costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))
                    .Sum(a => a.Cost),
                lastSevenDaysCost = costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)))
                    .Sum(a => a.Cost),
                lastThirtyDaysCost = costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)))
                    .Sum(a => a.Cost),
            },
        };

        var currency = costs.FirstOrDefault()?.Currency;
        var culture = CultureInfo.GetCultureInfo("en-US");
        var todaysDate = DateOnly.FromDateTime(DateTime.UtcNow);

        Console.WriteLine("# Azure Cost Overview");
        Console.WriteLine();
        Console.WriteLine(
            $"> Accumulated cost for subscription id `{settings.Subscription}` from **{costs.Min(a => a.Date)}** to **{costs.Max(a => a.Date)}**");
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

        var accumulatedCost = costs.OrderBy(x => x.Date).ToList();
        double accumulatedCostValue = 0.0;
        foreach (var day in accumulatedCost)
        {
            
            double costValue = day.Cost;
            accumulatedCostValue += costValue;
            
            Console.WriteLine($"   section {day.Date.ToString("dd MMM")}");
            Console.WriteLine($"   {currency} {Math.Round(accumulatedCostValue, 2):F2} :0, {Math.Round(accumulatedCostValue* 100, 0) }");
        }

        var forecastedData = forecastedCosts.Where(x => x.Date > accumulatedCost.Last().Date).OrderBy(x => x.Date)
            .ToList();
      
        foreach (var day in forecastedData)
        {
            double costValue = day.Cost;
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
        foreach (var cost in byServiceNameCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"|{cost.ItemName}|{cost.Cost:N2} {currency}|");
        }
        
        // Create a pie chart using mermaidjs
        Console.WriteLine();
        Console.WriteLine("```mermaid");
        Console.WriteLine("pie");
        Console.WriteLine("   title Cost by service");
        foreach (var cost in byServiceNameCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"   \"{cost.ItemName}\" : {cost.Cost.ToString("N2", culture)}");
        }
        Console.WriteLine("```");

        Console.WriteLine();
        Console.WriteLine("## By Location");
        Console.WriteLine();
        Console.WriteLine("|Location|Amount|");
        Console.WriteLine("|---|---:|");
        foreach (var cost in byLocationCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"|{cost.ItemName}|{cost.Cost:N2} {currency}|");
        }
        
        // Create a pie chart using mermaidjs
        Console.WriteLine();
        Console.WriteLine("```mermaid");
        Console.WriteLine("pie");
        Console.WriteLine("   title Cost by location");
        foreach (var cost in byLocationCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"   \"{cost.ItemName}\" : {cost.Cost.ToString("N2", culture)}");
        }
        Console.WriteLine("```");

        Console.WriteLine();
        Console.WriteLine("## By Resource Group");
        Console.WriteLine();
        Console.WriteLine("|Resource Group|Amount|");
        Console.WriteLine("|---|---:|");
        foreach (var cost in byResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"|{cost.ItemName}|{cost.Cost:N2} {currency}|");
        }

        // Generate a pie chart using mermaidjs
        Console.WriteLine();
        Console.WriteLine("```mermaid");
        Console.WriteLine("pie");
        Console.WriteLine("   title Cost by resource group");
        foreach (var cost in byResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"   \"{cost.ItemName}\" : {cost.Cost.ToString("N2", culture)}");
        }
        Console.WriteLine("```");
        
        Console.WriteLine();
        Console.WriteLine($"<sup>Generated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</sup>");

        return Task.CompletedTask;
    }

    public override Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources)
    {
        throw new NotImplementedException();
    }
}