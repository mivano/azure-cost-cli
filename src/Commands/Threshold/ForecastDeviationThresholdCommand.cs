using AzureCostCli.Commands.ShowCommand.OutputFormatters;
using AzureCostCli.CostApi;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Threshold;

public class ForecastDeviationThresholdCommand : BaseThresholdCommand
{
    private readonly ICostRetriever _costRetriever;

    public ForecastDeviationThresholdCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;
    }

    protected override async Task<ThresholdResult> PerformThresholdCheck(CommandContext context, Guid subscriptionId, ThresholdSettings settings)
    {
        AccumulatedCostDetails? accumulatedCost = null;
        ThresholdResult result = ThresholdResult.Empty;

        await AnsiConsole.Status()
            .StartAsync("Fetching cost data...", async ctx =>
            {
                ctx.Status = "Fetching subscription details...";
                // Fetch the subscription details
                var subscription = await _costRetriever.RetrieveSubscription(settings.Debug, subscriptionId);

                // Fetch the actual and forecasted costs here
                var costs = await _costRetriever.RetrieveCosts(settings.Debug, subscriptionId,
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
                    forecastedCosts = (await _costRetriever.RetrieveForecastedCosts(settings.Debug, subscriptionId,
                        settings.Filter,
                        settings.Metric,
                        TimeframeType.Custom,
                        forecastStartDate,
                        forecastEndDate)).ToList();
                }

                accumulatedCost = new AccumulatedCostDetails(
                    subscription,
                    costs,
                    forecastedCosts,
                    Enumerable.Empty<CostNamedItem>(),
                    Enumerable.Empty<CostNamedItem>(),
                    Enumerable.Empty<CostNamedItem>());

                ctx.Status = "Checking the forecast deviation...";
                result = CheckForecastDeviation(accumulatedCost, settings.Percentage, settings.FixedAmount);
            });

        return result;
    }

    private ThresholdResult CheckForecastDeviation(AccumulatedCostDetails details, double? percentageThreshold, double? fixedThreshold)
    {
        double actualTotal = details.Costs.Sum(c => c.Cost);
        double forecastTotal = details.ForecastedCosts.Sum(fc => fc.Cost);
            
        double deviationPercentage = ((actualTotal - forecastTotal) / forecastTotal) * 100;
        double deviationFixedAmount = actualTotal - forecastTotal;

        bool isExceeded =
            (percentageThreshold.HasValue && Math.Abs(deviationPercentage) >= percentageThreshold.Value) ||
            (fixedThreshold.HasValue && Math.Abs(deviationFixedAmount) >= fixedThreshold.Value);

        return new ThresholdResult(
            "forecast-deviation",
            isExceeded,
            deviationFixedAmount,
            fixedThreshold ?? percentageThreshold,
            $"Deviation between actual and forecasted cost is {deviationPercentage:F2}%, which equates to a fixed amount of {deviationFixedAmount:F2} {details.Costs.FirstOrDefault().Currency}."
        );
    }
}