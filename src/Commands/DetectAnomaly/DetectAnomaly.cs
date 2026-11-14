using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.DetectAnomaly;

public class DetectAnomalyCommand : AsyncCommand<DetectAnomalySettings>
{
    private readonly ICostRetriever _costRetriever;
    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = OutputFormatterFactory.Create();

    public DetectAnomalyCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;
    }

    public override ValidationResult Validate(CommandContext context, DetectAnomalySettings settings)
    {
        var subResult = CommandHelpers.ValidateAndResolveSubscription(
            settings.Subscription, settings.GetScope.IsSubscriptionBased,
            id => settings.Subscription = id);
        if (!subResult.Successful) return subResult;

        return CommandHelpers.ValidateTimeframe(settings);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DetectAnomalySettings settings, CancellationToken cancellationToken)
    {
        CommandHelpers.PrintVersionIfDebug(settings.Debug);
        _costRetriever.CostApiAddress = settings.CostApiAddress;
        _costRetriever.HttpTimeout = TimeSpan.FromSeconds(settings.HttpTimeout);

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