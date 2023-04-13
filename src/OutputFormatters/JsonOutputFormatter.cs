using System.Text.Json;
using Spectre.Console;

public class JsonOutputFormatter : OutputFormatter
{
    public override Task WriteOutput(ShowSettings settings, IEnumerable<CostItem> costs, IEnumerable<CostItem> forecastedCosts, IEnumerable<CostServiceItem> byServiceNameCosts)
    {
        var output = new
        {
            costs = new
            {
                todaysCost = costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow)).Sum(a => a.Cost),
                yesterdayCost = costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))).Sum(a => a.Cost),
                lastSevenDaysCost = costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7))).Sum(a => a.Cost),
                lastThirtyDaysCost = costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))).Sum(a => a.Cost),
            },
            forecastedCosts = forecastedCosts.Select(a=> new {a.Date, a.Cost}),
            byServiceNames = byServiceNameCosts.Select(a=> new {a.ServiceName, a.Cost})
        };
        
       Console.Write( JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
        
        return Task.CompletedTask;
    }
}