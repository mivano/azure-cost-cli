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
        Console.WriteLine(
            $"Azure Cost Overview for {settings.Subscription} by resource");

        Console.WriteLine();
            
        foreach (var resource in resources.OrderByDescending(a=>a.Cost))
        {
            if (settings.UseUSD)
            {
                Console.WriteLine(
                    $"{resource.ResourceId.Split('/').Last()} - {resource.ResourceType} - {resource.ResourceLocation} - {resource.ResourceGroupName} - {resource.CostUSD:N2} USD");

            }
            else
            {

                Console.WriteLine(
                    $"{resource.ResourceId.Split('/').Last()} - {resource.ResourceType} - {resource.ResourceLocation} - {resource.ResourceGroupName} - {resource.Cost:N2} {resource.Currency}");
            }
            
            foreach (var metered in resources
                         .Where(a=>a.ResourceId == resource.ResourceId)
                         .OrderByDescending(a=>a.Cost))
            {
                Console.WriteLine($"  {metered.ServiceName} - {metered.ServiceTier} - {metered.Meter} - {(settings.UseUSD ? metered.CostUSD :  metered.Cost):N2} {metered.Currency}");
            }
            
        }
      
        return Task.CompletedTask;
    }
}