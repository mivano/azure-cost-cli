using System.Text.Json;
using AzureCostCli.CostApi;
using DevLab.JmesPath;
using Spectre.Console;
using Spectre.Console.Json;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public class JsonOutputFormatter : BaseOutputFormatter
{
    public override Task WriteAccumulatedCost(AccumulatedCostSettings settings,AccumulatedCostDetails accumulatedCostDetails)
    {
        var output = new
        {
            totals = new
            {
                todaysCost = accumulatedCostDetails.Costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow)).Sum(a => a.Cost),
                yesterdayCost = accumulatedCostDetails.Costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))
                    .Sum(a => a.Cost),
                lastSevenDaysCost = accumulatedCostDetails.Costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)))
                    .Sum(a => a.Cost),
                lastThirtyDaysCost = accumulatedCostDetails.Costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)))
                    .Sum(a => a.Cost),
            },
            cost = accumulatedCostDetails.Costs.OrderBy(a => a.Date).Select(a => new { a.Date, a.Cost, a.Currency, a.CostUsd }),
            forecastedCosts = accumulatedCostDetails.ForecastedCosts.OrderByDescending(a => a.Date)
                .Select(a => new { a.Date, a.Cost,  a.Currency, a.CostUsd }),
            byServiceNames = accumulatedCostDetails.ByServiceNameCosts.OrderByDescending(a => a.Cost)
                .Select(a => new { ServiceName = a.ItemName, a.Cost, a.Currency, a.CostUsd }),
            ByLocation = accumulatedCostDetails.ByLocationCosts.OrderByDescending(a => a.Cost)
                .Select(a => new { Location = a.ItemName, a.Cost, a.Currency, a.CostUsd }),
            ByResourceGroup = accumulatedCostDetails.ByResourceGroupCosts.OrderByDescending(a => a.Cost)
                .Select(a => new { ResourceGroup = a.ItemName, a.Cost, a.Currency, a.CostUsd })
        };

        var json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });

        if (!string.IsNullOrWhiteSpace(settings.Query))
        {
            var jmes = new JmesPath();

            json = jmes.Transform(json, settings.Query);
        }

        switch (settings.Output)
        {
            case OutputFormat.Json:
                Console.Write(json);
                break;
            default:
                AnsiConsole.Write(
                    new JsonText(json)
                        .BracesColor(Color.Red)
                        .BracketColor(Color.Green)
                        .ColonColor(Color.Blue)
                        .CommaColor(Color.Red)
                        .StringColor(Color.Green)
                        .NumberColor(Color.Blue)
                        .BooleanColor(Color.Red)
                        .NullColor(Color.Green));
                break;
        }

        return Task.CompletedTask;
    }

    public override Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources)
    {
        var json = JsonSerializer.Serialize(resources, new JsonSerializerOptions { WriteIndented = true });

        if (!string.IsNullOrWhiteSpace(settings.Query))
        {
            var jmes = new JmesPath();

            json = jmes.Transform(json, settings.Query);
        }

        switch (settings.Output)
        {
            case OutputFormat.Json:
                Console.Write(json);
                break;
            default:
                AnsiConsole.Write(
                    new JsonText(json)
                        .BracesColor(Color.Red)
                        .BracketColor(Color.Green)
                        .ColonColor(Color.Blue)
                        .CommaColor(Color.Red)
                        .StringColor(Color.Green)
                        .NumberColor(Color.Blue)
                        .BooleanColor(Color.Red)
                        .NullColor(Color.Green));
                break;
        }

        return Task.CompletedTask;
    }
}