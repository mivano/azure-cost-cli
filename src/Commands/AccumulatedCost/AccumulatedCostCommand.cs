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
        // Check if we have any scope parameters when the scope requires subscription
        if (settings.GetScope.IsSubscriptionBased && !settings.Subscription.HasValue)
        {
            // Try to get subscription from Azure CLI
            try
            {
                var subscriptionId = Guid.Parse(AzCommand.GetDefaultAzureSubscriptionId());
                settings.Subscription = subscriptionId;
            }
            catch (Exception ex) when (ex is FormatException or ArgumentNullException or Exception)
            {
                // If we can't get the subscription from Azure CLI, return an error
                return ValidationResult.Error("No subscription ID provided and unable to retrieve from Azure CLI. Please specify a subscription ID using -s or --subscription, or login to Azure CLI using 'az login'. Use --help for more information.");
            }
        }
        
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

        _costRetriever.CostApiAddress = settings.CostApiAddress;
        _costRetriever.HttpTimeout = TimeSpan.FromSeconds(settings.HttpTimeout);

        // Get the subscription ID from the settings (already validated and set in Validate method)
        var subscriptionId = settings.Subscription;

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
                    ctx.Status = $"Fetching {settings.GetScope.Name} details...";

                    // Fetch the enrollment details //TODO

                    var enrollmentIdDisplayName = settings.EnrollmentAccountId != null ? $" {settings.EnrollmentAccountId}" : "";
                    var billingIdDisplayName = settings.BillingAccountId != null ? $" {settings.BillingAccountId}" : "";
                    subscription = new Subscription(string.Empty, string.Empty, Array.Empty<object>(), settings.GetScope.Name, settings.GetScope.Name, $"{settings.GetScope.Name} {enrollmentIdDisplayName} {billingIdDisplayName}", "Active", new SubscriptionPolicies(string.Empty, string.Empty, string.Empty));
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
                if (settings.GetScope.IsSubscriptionBased == false)
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

                ctx.Status = "Fetching cost data by resource group...";
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
            .WriteAccumulatedCost(settings, accumulatedCost);

        return 0;
    }
}