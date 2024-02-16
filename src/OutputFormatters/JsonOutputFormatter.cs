using System.Text.Json;
using System.Text.Json.Serialization;
using AzureCostCli.Commands;
using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.Commands.Budgets;
using AzureCostCli.Commands.CostByResource;
using AzureCostCli.Commands.CostByTag;
using AzureCostCli.Commands.DailyCost;
using AzureCostCli.Commands.DetectAnomaly;
using AzureCostCli.Commands.Regions;
using AzureCostCli.Commands.WhatIf;
using AzureCostCli.CostApi;
using DevLab.JmesPath;
using Spectre.Console;
using Spectre.Console.Json;

namespace AzureCostCli.OutputFormatters;

public class JsonOutputFormatter : BaseOutputFormatter
{
    public override Task WriteAccumulatedCost(AccumulatedCostSettings settings,AccumulatedCostDetails accumulatedCostDetails)
    {
        var output = new
        {
            totals = new
            {
                todaysCost = accumulatedCostDetails.Costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow)).Sum(a => settings.UseUSD ? a.CostUsd :  a.Cost),
                yesterdayCost = accumulatedCostDetails.Costs.Where(a => a.Date == DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)))
                    .Sum(a => settings.UseUSD ? a.CostUsd :  a.Cost),
                lastSevenDaysCost = accumulatedCostDetails.Costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)))
                    .Sum(a => settings.UseUSD ? a.CostUsd :  a.Cost),
                lastThirtyDaysCost = accumulatedCostDetails.Costs.Where(a => a.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)))
                    .Sum(a => settings.UseUSD ? a.CostUsd :  a.Cost),
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

        WriteJson(settings, output);
        
        return Task.CompletedTask;
    }

    public override Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources)
    {
        WriteJson(settings, resources);
        
        return Task.CompletedTask;
    }

    public override Task WriteBudgets(BudgetsSettings settings, IEnumerable<BudgetItem> budgets)
    {
        WriteJson(settings, budgets);

        return Task.CompletedTask;
    }

    public override Task WriteDailyCost(DailyCostSettings settings, IEnumerable<CostDailyItem> dailyCosts)
    {
        // Create a new variable to hold the dailyCost items per day
        // Code to avoid creating the column Tags when is not needed
        if (settings.IncludeTags == false)
        {
            var output = dailyCosts
                        .GroupBy(a => a.Date)
                        .Select(a => new
                        {
                            Date = a.Key,
                            Items = a.Select(b => new { b.Name, b.Cost, b.Currency, b.CostUsd})
                        });
            WriteJson(settings, output);
        }
        else
        {
            var output = dailyCosts
            .GroupBy(a => a.Date)
            .Select(a => new
            {
                Date = a.Key,
                Items = a.Select(b => new { b.Name, b.Cost, b.Currency, b.CostUsd, b.Tags})
            });
            WriteJson(settings, output);
        }
        return Task.CompletedTask;
    }

    public override Task WriteAnomalyDetectionResults(DetectAnomalySettings settings, List<AnomalyDetectionResult> anomalies)
    {
        WriteJson(settings, anomalies);

        return Task.CompletedTask;
    }

    public override Task WriteRegions(RegionsSettings settings, IReadOnlyCollection<AzureRegion> regions)
    {
        WriteJson(settings, regions);

        return Task.CompletedTask;
    }

    public override Task WriteCostByTag(CostByTagSettings settings, Dictionary<string, Dictionary<string, List<CostResourceItem>>> byTags)
    {
        WriteJson(settings, byTags);

        return Task.CompletedTask;
    }

    public override Task WritePricesPerRegion(WhatIfSettings settings, Dictionary<UsageDetails, List<PriceRecord>> pricesByRegion)
    {
        // We need to convert the dictionary to a list of objects with the properties of the CostResourceItem and then having a list of regions with their name and price
        var output = pricesByRegion.Select(a => new
        {
            UsageDetails = a.Key,
            Regions = a.Value
        });
        WriteJson(settings, output);
        
        return Task.CompletedTask;
    }
    

    private static void WriteJson(ICostSettings settings, object items)
    {

        var options = new JsonSerializerOptions { WriteIndented = true };
        
#if NET6_0
        options.Converters.Add(new DateOnlyJsonConverter());
#endif
        
        var json = JsonSerializer.Serialize(items, options );

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
    }

    
}

public sealed class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateOnly.Parse(reader.GetString()!);
    }

    public override DateOnly ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateOnly.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        var isoDate = value.ToString("O");
        writer.WriteStringValue(isoDate);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        var isoDate = value.ToString("O");
        writer.WritePropertyName(isoDate);
    }
}