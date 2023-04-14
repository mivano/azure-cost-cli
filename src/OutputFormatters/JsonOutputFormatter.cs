using System.Text.Json;
using Spectre.Console;

public class JsonOutputFormatter : OutputFormatter
{
    public override Task WriteOutput(ShowSettings settings, IEnumerable<CostItem> costs, IEnumerable<CostItem> forecastedCosts, IEnumerable<CostNamedItem> byServiceNameCosts,IEnumerable<CostNamedItem> byLocationCosts)
    {
        var output = new
        {
            totals = new
            {
                todaysCost = costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow)).Sum(a => a.Cost),
                yesterdayCost = costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))).Sum(a => a.Cost),
                lastSevenDaysCost = costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7))).Sum(a => a.Cost),
                lastThirtyDaysCost = costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))).Sum(a => a.Cost),
            },
            cost = costs.Select(a=> new {a.Date, a.Cost, a.Currency}),
            forecastedCosts = forecastedCosts.Select(a=> new {a.Date, a.Cost, a.Currency}),
            byServiceNames = byServiceNameCosts.Select(a=> new {ServiceName =a.ItemName, a.Cost, a.Currency}),
            ByLocation = byLocationCosts.Select(a=> new {Location = a.ItemName, a.Cost,  a.Currency})
        };
        
       Console.Write( JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
        
        return Task.CompletedTask;
    }
}