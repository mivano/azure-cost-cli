using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AzureCostCli.Commands.ShowCommand.OutputFormatters;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.ShowCommand;

public class DetectAnomalyCommand : AsyncCommand<DetectAnomalySettings>
{
    private readonly ICostRetriever _costRetriever;

    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = new();

    public DetectAnomalyCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;

        // Add the output formatters
        _outputFormatters.Add(OutputFormat.Console, new ConsoleOutputFormatter());
        _outputFormatters.Add(OutputFormat.Json, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Jsonc, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Text, new TextOutputFormatter());
        _outputFormatters.Add(OutputFormat.Markdown, new MarkdownOutputFormatter());
        _outputFormatters.Add(OutputFormat.Csv, new CsvOutputFormatter());
    }

    public override ValidationResult Validate(CommandContext context, DetectAnomalySettings settings)
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

        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DetectAnomalySettings settings)
    {
        // Show version
        if (settings.Debug)
            AnsiConsole.WriteLine($"Version: {typeof(AccumulatedCostCommand).Assembly.GetName().Version}");


        // Get the subscription ID from the settings
        var subscriptionId = settings.Subscription;

        if (subscriptionId == Guid.Empty)
        {
            // Get the subscription ID from the Azure CLI
            try
            {
                if (settings.Debug)
                    AnsiConsole.WriteLine(
                        "No subscription ID specified. Trying to retrieve the default subscription ID from Azure CLI.");

                subscriptionId = Guid.Parse(AzCommand.GetDefaultAzureSubscriptionId());

                if (settings.Debug)
                    AnsiConsole.WriteLine($"Default subscription ID retrieved from az cli: {subscriptionId}");

                settings.Subscription = subscriptionId;
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(new ArgumentException(
                    "Missing subscription ID. Please specify a subscription ID or login to Azure CLI.", e));
                return -1;
            }
        }

        // Change the settings so it uses a custom timeframe and fetch the last 30 days of data, not including today

        settings.Timeframe = TimeframeType.Custom;
        settings.From = DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        settings.To = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));

        // Fetch the costs from the Azure Cost Management API
        var dailyCost = await _costRetriever.RetrieveDailyCost(settings.Debug, subscriptionId,
            settings.Filter,
            settings.Dimension,
            settings.Timeframe,
            settings.From, settings.To);

        var costAnalyzer = new CostAnalyzer(settings);

        var anomalies = costAnalyzer.AnalyzeCost(dailyCost.ToList());

        // Write the output
        await _outputFormatters[settings.Output]
            .WriteAnomalyDetectionResults(settings, anomalies);

        return 0;
    }
}

public class CostAnalyzer
{
    private int RecentActivityDays = 7;
    private double SignificantChange = 0.5; // 50%
    private int SteadyGrowthDays = 7;

    public CostAnalyzer(DetectAnomalySettings settings)
    {
        RecentActivityDays = settings.RecentActivityDays;
        SignificantChange = settings.SignificantChange;
        SteadyGrowthDays = settings.SteadyGrowthDays;
    }

    public List<AnomalyDetectionResult> AnalyzeCost(List<CostDailyItem> items)
    {
        var groupedItems = items
            .GroupBy(i => i.Name)
            .Select(g => g.OrderBy(i => i.Date).ToList())
            .ToList();

        var results = new Dictionary<(string, AnomalyType), AnomalyDetectionResult>();
        foreach (var group in groupedItems)
        {
            AnomalyDetectionResult result;

            // Check for new costs
            if (group.First().Cost == 0 && group.Skip(1).Any(i => i.Cost != 0))
            {
                var startCostItem = group.First(i => i.Cost != 0);
                result = new AnomalyDetectionResult
                {
                    Name = startCostItem.Name,
                    DetectionDate = startCostItem.Date,
                    Message = "New cost detected",
                    CostDifference = startCostItem.Cost,
                    AnomalyType = AnomalyType.NewCost,
                    Data = group
                };
                results[(startCostItem.Name, AnomalyType.NewCost)] = result;
            }

            // Check for removed costs
            if (group.Last().Cost == 0)
            {
                for (int i = group.Count - 2; i >= 0; i--)
                {
                    if (group[i].Cost != 0)
                    {
                        var endCostItem = group[i];
                        result = new AnomalyDetectionResult
                        {
                            Name = endCostItem.Name,
                            DetectionDate = endCostItem.Date.AddDays(1), // Assuming the cost was removed the next day
                            Message = "Cost removed",
                            CostDifference = -endCostItem.Cost,
                            AnomalyType = AnomalyType.RemovedCost,
                            Data = group
                        };
                        results[(endCostItem.Name, AnomalyType.RemovedCost)] = result;
                        break;
                    }
                }
            }

            // Filter out inactive resources
            if (!IsResourceActive(group)) continue;

            // Check for significant cost changes
            for (int i = 1; i < group.Count; i++)
            {
                var today = group[i];
                var yesterday = group[i - 1];
                var diff = today.Cost - yesterday.Cost;

                if (Math.Abs(diff) / yesterday.Cost > SignificantChange)
                {
                    result = new AnomalyDetectionResult
                    {
                        Name = today.Name,
                        DetectionDate = today.Date,
                        Message = $"Significant cost change: from {yesterday.Cost} to {today.Cost} ({diff})",
                        CostDifference = diff,
                        AnomalyType = AnomalyType.SignificantChange,
                        Data = group
                    };
                    results[(today.Name, AnomalyType.SignificantChange)] = result;
                }
            }

            // Check for steady cost increase over a week
            if (IsSteadyCostIncrease(group))
            {
                var today = group.Last();
                result = new AnomalyDetectionResult
                {
                    Name = today.Name,
                    DetectionDate = today.Date,
                    Message = "Steady cost increase over a week",
                    CostDifference = today.Cost - group[^SteadyGrowthDays].Cost,
                    AnomalyType = AnomalyType.SteadyGrowth,
                    Data = group
                };
                results[(today.Name, AnomalyType.SteadyGrowth)] = result;
            }
        }

        return results.Values.ToList();
    }

    private bool IsResourceActive(IEnumerable<CostDailyItem> items)
    {
        return items.Any(i => i.Date >= DateOnly.FromDateTime(DateTime.Now.AddDays(-RecentActivityDays)));
    }

    private bool IsSteadyCostIncrease(IReadOnlyCollection<CostDailyItem> items)
    {
        if (items.Count < SteadyGrowthDays) return false;

        var lastWeekItems = items.TakeLast(SteadyGrowthDays).ToList();
        for (int i = 1; i < lastWeekItems.Count; i++)
        {
            if (lastWeekItems[i].Cost <= lastWeekItems[i - 1].Cost) return false;
        }

        return true;
    }
}


public record AnomalyDetectionResult
{
    public string Name { get; init; }
    public DateOnly DetectionDate { get; init; }
    public string Message { get; init; }
    public double CostDifference { get; init; }
    public AnomalyType AnomalyType { get; init; }
    public List<CostDailyItem> Data { get; set; }
}

public enum AnomalyType
{
    NewCost,
    RemovedCost,
    SignificantChange,
    SteadyGrowth
}