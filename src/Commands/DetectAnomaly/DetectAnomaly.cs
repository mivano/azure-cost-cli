using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.DetectAnomaly;

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
        // Automatically set timeframe to Custom if both from and to dates are provided
        settings.ApplyAutoTimeframe();
        
        // Validate if the timeframe is set to Custom, then the from date must be before the to date
        if (settings.Timeframe == TimeframeType.Custom)
        {
            if (settings.GetFromDate() > settings.GetToDate())
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
        _costRetriever.CostApiAddress = settings.CostApiAddress;
        _costRetriever.HttpTimeout = TimeSpan.FromSeconds(settings.HttpTimeout);

        // Get the subscription ID from the settings
        var subscriptionId = settings.Subscription;

        if (subscriptionId.HasValue == false && (settings.GetScope.IsSubscriptionBased))
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

        // Fetch the costs from the Azure Cost Management API
        var dailyCost = await _costRetriever.RetrieveDailyCost(settings.Debug, settings.GetScope,
            settings.Filter,
            settings.Metric,
            settings.Dimension,
            settings.Timeframe,
            settings.GetFromDate(), settings.GetToDate(),
            false);

        var costAnalyzer = new CostAnalyzer(settings);

        var anomalies = costAnalyzer.AnalyzeCost(dailyCost.ToList());

        // Write the output
        await _outputFormatters[settings.Output]
            .WriteAnomalyDetectionResults(settings, anomalies);

        return 0;
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