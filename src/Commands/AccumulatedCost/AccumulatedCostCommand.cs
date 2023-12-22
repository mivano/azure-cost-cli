using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.AccumulatedCost;

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
        _outputFormatters.Add(OutputFormat.Csv, new CsvOutputFormatter());
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

        if (subscriptionId.HasValue == false && (settings.GetScope.IsSubscriptionBased))
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

        AccumulatedCostDetails accumulatedCost = null;

        Subscription subscription = null;
       
        await AnsiConsoleExt.Status()
            .StartAsync("Fetching cost data...", async ctx =>
            {
                
                if (settings.GetScope.IsSubscriptionBased)
                {
                    ctx.Status = "Fetching subscription details...";
                    // Fetch the subscription details
                    subscription = await _costRetriever.RetrieveSubscription(settings.Debug, subscriptionId.Value);
                }
                else 
                {
                    ctx.Status = "Fetching Enrollment details...";
                    // Fetch the enrollment details //TODO
                    subscription = new Subscription(string.Empty, string.Empty, Array.Empty<object>(), "Enrollment", "Enrollment", $"Enrollment {settings.EnrollmentAccountId}", "Active", new SubscriptionPolicies(string.Empty, string.Empty, string.Empty));
                }

                ctx.Status = "Fetching cost data...";
                // Fetch the costs from the Azure Cost Management API
                var costs = await _costRetriever.RetrieveCosts(settings.Debug, settings.GetScope,
                    settings.Filter,
                    settings.Metric,
                    settings.Timeframe,
                    settings.From, settings.To);

                List<CostItem> forecastedCosts = new List<CostItem>();

                // Check if the 'settings.To' date is equal to or greater than today's date
                DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
                DateOnly forecastStartDate;

                switch (settings.Timeframe)
                {
                    case TimeframeType.BillingMonthToDate:
                    case TimeframeType.MonthToDate:
                        forecastStartDate =
                            DateOnly.FromDateTime(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
                        break;
                    case TimeframeType.TheLastBillingMonth:
                    case TimeframeType.TheLastMonth:
                        forecastStartDate =
                            DateOnly.FromDateTime(
                                new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1));
                        break;
                    case TimeframeType.WeekToDate:
                        forecastStartDate =
                            DateOnly.FromDateTime(DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek));
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

                    ctx.Status = "Fetching forecasted cost data...";
                    forecastedCosts = (await _costRetriever.RetrieveForecastedCosts(settings.Debug, settings.GetScope,
                        settings.Filter,
                        settings.Metric,
                        TimeframeType.Custom,
                        forecastStartDate,
                        forecastEndDate)).ToList();
                }

                IEnumerable<CostNamedItem> bySubscriptionCosts = null;
                if (settings.GetScope.IsSubscriptionBased==false)
                {
                    ctx.Status = "Fetching cost data by subscription...";
                    bySubscriptionCosts = await _costRetriever.RetrieveCostBySubscription(settings.Debug,
                        settings.GetScope, settings.Filter, settings.Metric, settings.Timeframe, settings.From, settings.To);
                }

                ctx.Status = "Fetching cost data by service name...";
                var byServiceNameCosts = await _costRetriever.RetrieveCostByServiceName(settings.Debug,
                    settings.GetScope, settings.Filter, settings.Metric, settings.Timeframe, settings.From, settings.To);
                
                ctx.Status = "Fetching cost data by location...";
                var byLocationCosts = await _costRetriever.RetrieveCostByLocation(settings.Debug, settings.GetScope,
                    settings.Filter,
                    settings.Metric,
                    settings.Timeframe, settings.From, settings.To);
                
                ctx.Status= "Fetching cost data by resource group...";
                var byResourceGroupCosts = await _costRetriever.RetrieveCostByResourceGroup(settings.Debug,
                    settings.GetScope,
                    settings.Filter,
                    settings.Metric,
                    settings.Timeframe, settings.From, settings.To);

                accumulatedCost = new AccumulatedCostDetails(subscription, null, costs, forecastedCosts, byServiceNameCosts,
                    byLocationCosts, byResourceGroupCosts, bySubscriptionCosts);

            });
        
        // Write the output
        await _outputFormatters[settings.Output]
            .WriteAccumulatedCost(settings,accumulatedCost);

        return 0;
    }
}