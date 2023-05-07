using System.Diagnostics;
using System.Text.Json;
using AzureCostCli.Commands.ShowCommand.OutputFormatters;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.ShowCommand;

public class AccumulatedCostCommand : AsyncCommand<AccumulatedCostSettings>
{
    private readonly ICostRetriever _costRetriever;

    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = new();

    public AccumulatedCostCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;
        
        // Add the output formatters
        _outputFormatters.Add(OutputFormat.Console, new ConsoleOutputFormatter());
        _outputFormatters.Add(OutputFormat.Json, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Jsonc, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Text, new TextOutputFormatter());
        _outputFormatters.Add(OutputFormat.Markdown, new MarkdownOutputFormatter());
    }

    public override ValidationResult Validate(CommandContext context, AccumulatedCostSettings settings)
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

    public override async Task<int> ExecuteAsync(CommandContext context, AccumulatedCostSettings settings)
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

        // Fetch the costs from the Azure Cost Management API
        var costs = await _costRetriever.RetrieveCosts(settings.Debug, subscriptionId, settings.Timeframe,
            settings.From, settings.To);
       
        List<CostItem> forecastedCosts = new List<CostItem>();

        // Find the maximum date of the retrieved costs
        DateOnly maxRetrievedCostDate = costs.Max(a => a.Date);

        // Check if the 'settings.To' date is equal to or greater than today's date
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly forecastStartDate;

        switch (settings.Timeframe)
        {
            case TimeframeType.BillingMonthToDate:
            case TimeframeType.MonthToDate:
                forecastStartDate = DateOnly.FromDateTime(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
                break;
            case TimeframeType.TheLastBillingMonth:
            case TimeframeType.TheLastMonth:
                forecastStartDate = DateOnly.FromDateTime(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1));
                break;
            case TimeframeType.WeekToDate:
                forecastStartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek));
                break;
            default:
                // Custom Timeframe
                forecastStartDate = settings.To >= today ? today : default;
                break;
        }

        if (forecastStartDate != default)
        {
            DateOnly forecastEndDate = new DateOnly(settings.To.Year, settings.To.Month,
                DateTime.DaysInMonth(settings.To.Year, settings.To.Month));

            forecastedCosts = (await _costRetriever.RetrieveForecastedCosts(settings.Debug, subscriptionId,
                TimeframeType.Custom,
                forecastStartDate,
                forecastEndDate)).ToList();
        }
        
        var byServiceNameCosts =  await _costRetriever.RetrieveCostByServiceName(settings.Debug,
            subscriptionId, settings.Timeframe, settings.From, settings.To);
        var byLocationCosts =  await _costRetriever.RetrieveCostByLocation(settings.Debug, subscriptionId,
            settings.Timeframe, settings.From, settings.To);
        var byResourceGroupCosts =  await _costRetriever.RetrieveCostByResourceGroup(settings.Debug, subscriptionId,
            settings.Timeframe, settings.From, settings.To);

      
        // Write the output
        await _outputFormatters[settings.Output]
            .WriteAccumulatedCost(settings, costs, forecastedCosts, byServiceNameCosts, byLocationCosts, byResourceGroupCosts);

        return 0;
    }
}