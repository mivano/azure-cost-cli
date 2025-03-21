using System.Text.Json;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Diff;

public class DiffCommand : AsyncCommand<DiffSettings>
{
    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = new();

    public DiffCommand()
    {
        // Add the output formatters
        _outputFormatters.Add(OutputFormat.Console, new ConsoleOutputFormatter());
        _outputFormatters.Add(OutputFormat.Json, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Jsonc, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Text, new TextOutputFormatter());
        _outputFormatters.Add(OutputFormat.Markdown, new MarkdownOutputFormatter());
        _outputFormatters.Add(OutputFormat.Csv, new CsvOutputFormatter());
    }

    public override ValidationResult Validate(CommandContext context, DiffSettings settings)
    {
        // Validate if the timeframe is set to Custom, then the from and to dates must be specified and the from date must be before the to date
        if (settings.Timeframe == TimeframeType.Custom)
        {
            if (settings.From == null)
            {
                return ValidationResult.Error("The from date must be specified when the timeframe is set to Custom.");
            }

            if (settings.To == null)
            {
                return ValidationResult.Error("The to date must be specified when the timeframe is set to Custom.");
            }

            if (settings.From > settings.To)
            {
                return ValidationResult.Error("The from date must be before the to date.");
            }
        }

        const string CompareToMessage =
            "The compare to file does not exist or is not specified. Please create the json file by running `azure-cost accumulatedCost -o json > filename.json`";
        const string CompareFromMessage =
            "The compare from file does not exist or is not specified. Please create the json file by running `azure-cost accumulatedCost -o json > filename.json`";

        if (string.IsNullOrEmpty(settings.CompareTo))
        {
            return ValidationResult.Error(CompareToMessage);
        }

        if (settings.CompareTo.EndsWith(".json") == false)
        {
            return ValidationResult.Error(CompareToMessage);
        }

        if (Path.Exists(settings.CompareTo) == false)
        {
            return ValidationResult.Error(CompareToMessage);
        }

        if (string.IsNullOrEmpty(settings.CompareFrom))
        {
            return ValidationResult.Error(CompareFromMessage);
        }

        if (settings.CompareFrom.EndsWith(".json") == false)
        {
            return ValidationResult.Error(CompareFromMessage);
        }

        if (Path.Exists(settings.CompareFrom) == false)
        {
            return ValidationResult.Error(CompareFromMessage);
        }


        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DiffSettings settings)
    {
        // Show version
        if (settings.Debug)
            AnsiConsole.WriteLine($"Version: {typeof(DiffCommand).Assembly.GetName().Version}");

        AccumulatedCostDetails accumulatedCostSource = null;
        AccumulatedCostDetails accumulatedCostTarget = null;

        await AnsiConsoleExt.Status()
            .StartAsync("Reading data", async ctx =>
            {
                accumulatedCostSource = await ReadAccumulatedCost(settings.CompareFrom);
                accumulatedCostTarget = await ReadAccumulatedCost(settings.CompareTo);
            });

        // Write the output
        await _outputFormatters[settings.Output]
            .WriteAccumulatedDiffCost(settings, accumulatedCostSource, accumulatedCostTarget);

        return 0;
    }

    private async Task<AccumulatedCostDetails> ReadAccumulatedCost(string file)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
            var content = await File.ReadAllTextAsync(file);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var cost = root.GetProperty("cost").Deserialize<List<CostDetail>>(options);
            var forecastedCosts = root.GetProperty("forecastedCosts").Deserialize<List<CostDetail>>(options);
            
            // Use JsonElement to directly access the correct property names in each object
            var byServiceNames = root.GetProperty("byServiceNames").EnumerateArray()
                .Select(item => new CostNamedItem(
                    item.GetProperty("ServiceName").GetString(),
                    item.GetProperty("Cost").GetDouble(),
                    item.GetProperty("CostUsd").GetDouble(),
                    item.GetProperty("Currency").GetString()))
                .ToList();

            var byLocationCosts = root.GetProperty("ByLocation").EnumerateArray()
                .Select(item => new CostNamedItem(
                    item.GetProperty("Location").GetString(),
                    item.GetProperty("Cost").GetDouble(),
                    item.GetProperty("CostUsd").GetDouble(),
                    item.GetProperty("Currency").GetString()))
                .ToList();

            var byResourceGroupCosts = root.GetProperty("ByResourceGroup").EnumerateArray()
                .Select(item => new CostNamedItem(
                    item.GetProperty("ResourceGroup").GetString(),
                    item.GetProperty("Cost").GetDouble(),
                    item.GetProperty("CostUsd").GetDouble(),
                    item.GetProperty("Currency").GetString()))
                .ToList();
           
            return new AccumulatedCostDetails(null, null,
                Costs: cost.Select(a => new CostItem(a.Date, a.Cost, a.CostUsd, a.Currency)).ToList(),
                ForecastedCosts: forecastedCosts.Select(a => new CostItem(a.Date, a.Cost, a.CostUsd, a.Currency))
                    .ToList(),
                ByServiceNameCosts: byServiceNames
                    .Select(a => new CostNamedItem(a.ItemName, a.Cost, a.CostUsd, a.Currency)).ToList(),
                ByLocationCosts: byLocationCosts
                    .Select(a => new CostNamedItem(a.ItemName, a.Cost, a.CostUsd, a.Currency)).ToList(),
                ByResourceGroupCosts: byResourceGroupCosts
                    .Select(a => new CostNamedItem(a.ItemName, a.Cost, a.CostUsd, a.Currency)).ToList(),
                null);
        }
        catch (Exception e)
        {
            throw new Exception($"Error reading the accumulated cost file: {file}", e);
        }
    }
}

internal class CostDetail
{
    public DateOnly Date { get; set; }
    public string ItemName { get; set; }
    public double Cost { get; set; }
    public double CostUsd { get; set; }
    public string Currency { get; set; }
}