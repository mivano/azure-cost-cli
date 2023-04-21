using System.Text.Json;
using AzureCostCli.CostApi;
using DevLab.JmesPath;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public class JsonOutputFormatter : OutputFormatter
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
            totals = new
            {
                todaysCost = costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow)).Sum(a => a.Cost),
                yesterdayCost = costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))).Sum(a => a.Cost),
                lastSevenDaysCost = costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7))).Sum(a => a.Cost),
                lastThirtyDaysCost = costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))).Sum(a => a.Cost),
            },
            cost = costs.OrderBy(a=>a.Date).Select(a=> new {a.Date, a.Cost, a.Currency}),
            forecastedCosts = forecastedCosts.OrderByDescending(a=>a.Date).Select(a=> new {a.Date, a.Cost, a.Currency}),
            byServiceNames = byServiceNameCosts.OrderByDescending(a=>a.Cost).Select(a=> new {ServiceName =a.ItemName, a.Cost, a.Currency}),
            ByLocation = byLocationCosts.OrderByDescending(a=>a.Cost).Select(a=> new {Location = a.ItemName, a.Cost,  a.Currency}),
            ByResourceGroup = byResourceGroupCosts.OrderByDescending(a=>a.Cost).Select(a=> new {ResourceGroup = a.ItemName, a.Cost, a.Currency})
        };

        var json =JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        
        if (!string.IsNullOrWhiteSpace(settings.Query))
        {
            var jmes = new JmesPath();
            
            var result = jmes.Transform(json, settings.Query);
            
            Console.Write(result);
        }
        else
        {
            Console.Write(json);
        }
        
        return Task.CompletedTask;
    }
}