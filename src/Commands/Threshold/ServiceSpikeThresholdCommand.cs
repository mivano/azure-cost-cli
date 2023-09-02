using System.Text;
using AzureCostCli.CostApi;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Threshold;

public class ServiceSpikeThresholdCommand : BaseThresholdCommand
{
    private readonly ICostRetriever _costRetriever;

    public ServiceSpikeThresholdCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;
    }

    protected override async Task<ThresholdResult> PerformThresholdCheck(CommandContext context, Guid subscriptionId,
        ThresholdSettings settings)
    {
        ThresholdResult result = ThresholdResult.Empty;

        await AnsiConsole.Status()
            .StartAsync("Fetching cost data by service...", async ctx =>
            {
                ctx.Status = "Fetching service cost data...";
                // Fetch aggregated costs by service name for the current period
                var currentCostByServices = await _costRetriever.RetrieveCostByServiceName(settings.Debug, subscriptionId, settings.Filter, settings.Metric, settings.Timeframe, settings.From, settings.To);

                
                ctx.Status = "Fetching previous service cost data...";
                var (previousFrom, previousTo) = GetPreviousPeriodDates(settings.Timeframe, settings.From, settings.To);
                // Fetch aggregated costs by service name for the previous period
                var previousCostByServices = await _costRetriever.RetrieveCostByServiceName(settings.Debug, subscriptionId, settings.Filter, settings.Metric, settings.Timeframe, previousFrom, previousTo);
            
                ctx.Status = "Checking for service spikes...";

                // Call to CheckServiceSpike function
                var serviceSpikeResult = CheckServiceSpike(currentCostByServices, previousCostByServices, settings.FixedAmount, settings.Percentage);

            });

        return result;
    }

    private ThresholdResult CheckServiceSpike(IEnumerable<CostNamedItem> currentCostByServices,IEnumerable<CostNamedItem> previousCostByServices, double? fixedThreshold, double? percentageThreshold)
    {
        // Loop through the currentCostByServices to check for any spikes in the cost
        foreach (var currentServiceCost in currentCostByServices)
        {
            var previousServiceCost = previousCostByServices.FirstOrDefault(p => p.ItemName == currentServiceCost.ItemName);

            if (previousServiceCost != null)
            {
                double changePercentage = ((currentServiceCost.Cost - previousServiceCost.Cost) / previousServiceCost.Cost) * 100;
            
                if (percentageThreshold.HasValue && Math.Abs(changePercentage) >= percentageThreshold.Value)
                {
                    return new ThresholdResult(
                        "service-spike",
                        true,
                        currentServiceCost.Cost,
                        percentageThreshold,
                        $"Cost spike detected in service: {currentServiceCost.ItemName}. The cost change is {changePercentage:F2}%, which exceeds the percentage threshold of {percentageThreshold.Value}%."
                    );
                }
            }
        
            if (fixedThreshold.HasValue && currentServiceCost.Cost >= fixedThreshold.Value)
            {
                return new ThresholdResult(
                    "service-spike",
                    true,
                    currentServiceCost.Cost,
                    fixedThreshold,
                    $"Cost spike detected in service: {currentServiceCost.ItemName}. The cost is {currentServiceCost.Cost} {currentServiceCost.Currency}, which exceeds the fixed threshold of {fixedThreshold.Value} {currentServiceCost.Currency}."
                );
            }
        }

        return new ThresholdResult("service-spike", false, null, null, "No service exceeded the defined thresholds.");

    }
    
    public (DateOnly previousFrom, DateOnly previousTo) GetPreviousPeriodDates(TimeframeType timeframe, DateOnly from, DateOnly to)
    {
        DateOnly previousFrom;
        DateOnly previousTo;

        switch (timeframe)
        {
            case TimeframeType.Custom:
                int durationDays = (to.DayNumber - from.DayNumber);
                previousFrom = from.AddDays(-durationDays);
                previousTo = to.AddDays(-durationDays);
                break;

            case TimeframeType.BillingMonthToDate:
            case TimeframeType.MonthToDate:
                previousFrom = DateOnly.FromDateTime(DateTime.Today.AddMonths(-1));
                previousTo = DateOnly.FromDateTime(DateTime.Today);
                break;

            case TimeframeType.TheLastBillingMonth:
            case TimeframeType.TheLastMonth:
                DateTime firstDayOfThisMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                previousFrom = DateOnly.FromDateTime(firstDayOfThisMonth.AddMonths(-1));
                previousTo = DateOnly.FromDateTime(firstDayOfThisMonth.AddDays(-1));
                break;

            case TimeframeType.WeekToDate:
                previousFrom = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
                previousTo = DateOnly.FromDateTime(DateTime.Today);
                break;

            default:
                throw new ArgumentException($"Unsupported timeframe type: {timeframe}");
        }

        return (previousFrom, previousTo);
    }


}