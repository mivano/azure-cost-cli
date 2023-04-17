using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public class TextOutputFormatter : OutputFormatter
{
    public override Task WriteOutput(ShowSettings settings, 
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
        
        Console.WriteLine(
            $"Azure Cost Overview for {settings.Subscription} from {costs.Min(a => a.Date)} to {costs.Max(a => a.Date)}");
        Console.WriteLine();
        Console.WriteLine("Totals:");
        Console.WriteLine($"  Today: {output.costs.todaysCost:N2} {currency}");
        Console.WriteLine($"  Yesterday: {output.costs.yesterdayCost:N2} {currency}");
        Console.WriteLine($"  Last 7 days: {output.costs.lastSevenDaysCost:N2} {currency}");
        Console.WriteLine($"  Last 30 days: {output.costs.lastThirtyDaysCost:N2} {currency}");
        
        Console.WriteLine();
        Console.WriteLine("By Service Name:");
        foreach (var cost in byServiceNameCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"  {cost.ItemName}: {cost.Cost:N2} {currency}");
        }

        Console.WriteLine();
        Console.WriteLine("By Location:");
        foreach (var cost in byLocationCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"  {cost.ItemName}: {cost.Cost:N2} {currency}");
        }

        Console.WriteLine();
        Console.WriteLine("By Resource Group:");
        foreach (var cost in byResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
        {
            Console.WriteLine($"  {cost.ItemName}: {cost.Cost:N2} {currency}");
        }
        
        return Task.CompletedTask;
    }
}