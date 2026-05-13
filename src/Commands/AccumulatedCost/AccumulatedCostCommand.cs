using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.AccumulatedCost;

public class AccumulatedCostCommand : AsyncCommand<AccumulatedCostSettings>
{
    private readonly ICostRetriever _costRetriever;
    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = OutputFormatterFactory.Create();

    public AccumulatedCostCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;
    }

    protected override ValidationResult Validate(CommandContext context, AccumulatedCostSettings settings)
    {
        var subResult = CommandHelpers.ValidateAndResolveSubscription(
            settings.Subscription, settings.GetScope.IsSubscriptionBased,
            id => settings.Subscription = id);
        if (!subResult.Successful) return subResult;

        return CommandHelpers.ValidateTimeframe(settings);
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, AccumulatedCostSettings settings, CancellationToken cancellationToken)
    {
        CommandHelpers.PrintVersionIfDebug(settings.Debug);

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
                    settings.GetFromDate(), settings.GetToDate());

                List<CostItem> forecastedCosts = new List<CostItem>();

                // Check if the 'settings.To' date is equal to or greater than today's date
                DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
                DateOnly forecastStartDate;
                DateOnly toDate = settings.GetToDate();

                switch (settings.Timeframe)
                {
                    case TimeframeType.BillingMonthToDate:
                    case TimeframeType.MonthToDate:
                        forecastStartDate =
                            DateOnly.FromDateTime(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
                        break;
                    case TimeframeType.TheLastBillingMonth:
                    case TimeframeType.TheLastMonth:
                        // These timeframes are entirely in the past; no forecast needed
                        forecastStartDate = default;
                        break;
                    case TimeframeType.WeekToDate:
                        forecastStartDate =
                            DateOnly.FromDateTime(DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek));
                        break;
                    default:
                        // Custom Timeframe
                        forecastStartDate = toDate >= today ? today : default;
                        break;
                }

                if (forecastStartDate != default)
                {
                    DateOnly forecastEndDate = new DateOnly(toDate.Year, toDate.Month,
                        DateTime.DaysInMonth(toDate.Year, toDate.Month));

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
                        settings.GetScope, settings.Filter, settings.Metric, settings.Timeframe, settings.GetFromDate(), settings.GetToDate());
                }

                ctx.Status = "Fetching cost data by service name...";
                var byServiceNameCosts = await _costRetriever.RetrieveCostByServiceName(settings.Debug,
                    settings.GetScope, settings.Filter, settings.Metric, settings.Timeframe, settings.GetFromDate(), settings.GetToDate());

                ctx.Status = "Fetching cost data by location...";
                var byLocationCosts = await _costRetriever.RetrieveCostByLocation(settings.Debug, settings.GetScope,
                    settings.Filter,
                    settings.Metric,
                    settings.Timeframe, settings.GetFromDate(), settings.GetToDate());

                ctx.Status = "Fetching cost data by resource group...";
                var byResourceGroupCosts = await _costRetriever.RetrieveCostByResourceGroup(settings.Debug,
                    settings.GetScope,
                    settings.Filter,
                    settings.Metric,
                    settings.Timeframe, settings.GetFromDate(), settings.GetToDate());

                accumulatedCost = new AccumulatedCostDetails(subscription, null, costs, forecastedCosts, byServiceNameCosts,
                    byLocationCosts, byResourceGroupCosts, bySubscriptionCosts);

            });

        // Write the output
        await _outputFormatters[settings.Output]
            .WriteAccumulatedCost(settings, accumulatedCost);

        // Check cost threshold after output
        var totalCost = accumulatedCost.Costs.Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
        var currency = settings.UseUSD ? "USD" : accumulatedCost.Costs.FirstOrDefault()?.Currency ?? "USD";
        return CommandHelpers.CheckCostThreshold(totalCost, settings.FailIfOver, currency);
    }
}