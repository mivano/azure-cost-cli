using System.Diagnostics;
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
                    AnsiConsole.WriteLine("No subscription ID specified. Trying to retrieve the default subscription ID from Azure CLI.");
                
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

        // Fetch the subscription details
        var subscription = await _costRetriever.RetrieveSubscription(settings.Debug, subscriptionId);

        // Select the last 60 days of data
        settings.Timeframe = TimeframeType.Custom;
        settings.From = DateOnly.FromDateTime(DateTime.Today.AddDays(-60));
        settings.To = DateOnly.FromDateTime(DateTime.Today);
        
        // Fetch the costs from the Azure Cost Management API
        var dailyCost = await _costRetriever.RetrieveDailyCost(settings.Debug, subscriptionId, 
            settings.Filter,
            "ResourceId",
            settings.Timeframe,
            settings.From, settings.To);
        
        var costAnalyzer = new CostAnalyzer(dailyCost.ToList());

        var anomalies = costAnalyzer.DetectAnomalies();
        
        // Write the output
      //  await _outputFormatters[settings.Output]
      //      .WriteDailyCost(settings, dailyCost);

      foreach (var anomaly in anomalies.Where(a=>a.Type!="No cost"))
      {
          Console.WriteLine($"{anomaly.Date} {anomaly.ResourceName} {anomaly.ChangeInPercent}%  {anomaly.Type} {anomaly.Description}");
      }
      
        return 0;
    }
}

public record AnomalyDetectionResult(
    DateOnly Date, 
    string ResourceName, 
    double CurrentCost, 
    double PreviousCost, 
    double ChangeInPercent, 
    double ZScore, 
    string Type,
    string Description);
public class CostAnalyzer
{
    private List<CostDailyItem> _costData;
    private const int MovingAveragePeriod = 30;
    private const double ZScoreThreshold = 3;
    private const double NoCostThreshold = 0;
    private const double RunRateThreshold = 2;  // cost is double the average run rate

    public CostAnalyzer(List<CostDailyItem> costData)
    {
        _costData = costData.OrderBy(c => c.Date).ToList();
    }

    public List<AnomalyDetectionResult> DetectAnomalies()
    {
        var anomalies = new List<AnomalyDetectionResult>();
        var costMean = _costData.Average(c => c.Cost);
        var costStdDev = CalculateStandardDeviation(_costData.Select(c => c.Cost).ToList());

        for (int i = MovingAveragePeriod; i < _costData.Count; i++)
        {
            var previousCostData = _costData[i - 1];
            var currentCostData = _costData[i];

            var dailyRunRate = _costData.Skip(i - MovingAveragePeriod).Take(MovingAveragePeriod).Average(c => c.Cost);
            
            var zScore = (currentCostData.Cost - costMean) / costStdDev;
            var changeInPercent = (currentCostData.Cost - previousCostData.Cost) / previousCostData.Cost * 100;
            
            string type = "";
            string description = "";

            if (Math.Abs(zScore) > ZScoreThreshold)
            {
                type = zScore > 0 ? "Spike" : "Drop";
                description = $"The cost changed from {previousCostData.Cost} to {currentCostData.Cost}, which is a {changeInPercent}% change.";
                anomalies.Add(new AnomalyDetectionResult(currentCostData.Date, currentCostData.Name, currentCostData.Cost, previousCostData.Cost, changeInPercent, zScore, type, description));
            }
            else if (currentCostData.Cost == NoCostThreshold)
            {
                type = "No cost";
                description = $"The cost changed from {previousCostData.Cost} to 0, which indicates that this resource is no longer generating any cost.";
                anomalies.Add(new AnomalyDetectionResult(currentCostData.Date, currentCostData.Name, currentCostData.Cost, previousCostData.Cost, -100, 0, type, description));
            }
            else if (currentCostData.Cost > RunRateThreshold * dailyRunRate)
            {
                type = "High run rate";
                description = $"The cost of {currentCostData.Cost} is significantly higher than the average daily run rate of {dailyRunRate} over the last {MovingAveragePeriod} days.";
                anomalies.Add(new AnomalyDetectionResult(currentCostData.Date, currentCostData.Name, currentCostData.Cost, previousCostData.Cost, changeInPercent, zScore, type, description));
            }
        }

        return anomalies;
    }

    private double CalculateStandardDeviation(List<double> values)
    {
        var average = values.Average();
        var sumOfSquaresOfDifferences = values.Select(val => (val - average) * (val - average)).Sum();
        return Math.Sqrt(sumOfSquaresOfDifferences / values.Count);
    }
}



