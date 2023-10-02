using AzureCostCli.Commands.ShowCommand.OutputFormatters;
using AzureCostCli.CostApi;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Threshold;

public class WeeklyAverageThresholdCommand : BaseThresholdCommand
{
    private readonly ICostRetriever _costRetriever;

    public WeeklyAverageThresholdCommand(ICostRetriever costRetriever)
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
                // Similar code to fetch the costs but this time for a week
                var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
                var toDate = DateOnly.FromDateTime(DateTime.UtcNow);

                ctx.Status = "Fetching cost data...";
                var costs = await _costRetriever.RetrieveCosts(settings.Debug, subscriptionId,
                    settings.Filter,
                    settings.Metric,
                    TimeframeType.Custom,
                    fromDate, toDate);

                accumulatedCost = new AccumulatedCostDetails(
                    null,
                    costs,
                    new List<CostItem>(),
                    Enumerable.Empty<CostNamedItem>(),
                    Enumerable.Empty<CostNamedItem>(),
                    Enumerable.Empty<CostNamedItem>());

                ctx.Status = "Calculating the weekly average...";
                result = CheckWeeklyAverage(accumulatedCost, settings.Percentage, settings.FixedAmount);
            });

        return result;
    }

    private ThresholdResult CheckWeeklyAverage(AccumulatedCostDetails details, double? percentageThreshold, double? fixedThreshold)
    {
        var lastWeek = details.Costs.Where(c => c.Date >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)));
        var weeklyAverage = lastWeek.Average(c => c.Cost);

        bool isExceeded =
            (percentageThreshold.HasValue && weeklyAverage >= percentageThreshold.Value) ||
            (fixedThreshold.HasValue && weeklyAverage >= fixedThreshold.Value);

        return new ThresholdResult(
            "weekly-average",
            isExceeded,
            weeklyAverage,
            fixedThreshold ?? percentageThreshold,
            $"The weekly average cost is {weeklyAverage:F2} {details.Costs.FirstOrDefault().Currency}."
        );
    }
}