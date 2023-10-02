using AzureCostCli.Commands.ShowCommand.OutputFormatters;
using AzureCostCli.CostApi;

namespace AzureCostCli.Commands.Threshold;

using Spectre.Console;
using Spectre.Console.Cli;
using System.Threading.Tasks;

public class DailyChangeThresholdCommand : BaseThresholdCommand
{
    private readonly ICostRetriever _costRetriever;

    public DailyChangeThresholdCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;
    }

    protected override async Task<ThresholdResult> PerformThresholdCheck(CommandContext context, Guid subscriptionId,
        ThresholdSettings settings)
    {
        AccumulatedCostDetails? accumulatedCost = null;
        ThresholdResult result = ThresholdResult.Empty;

        await AnsiConsole.Status()
            .StartAsync("Fetching cost data...", async ctx =>
            {
                ctx.Status = "Fetching subscription details...";
                // Fetch the subscription details
                var subscription = await _costRetriever.RetrieveSubscription(settings.Debug, subscriptionId);

                ctx.Status = "Fetching cost data...";
                // Fetch the costs from the Azure Cost Management API
                var costs = await _costRetriever.RetrieveCosts(settings.Debug, subscriptionId,
                    settings.Filter,
                    settings.Metric,
                    settings.Timeframe,
                    settings.From, settings.To);

                List<CostItem> forecastedCosts = new List<CostItem>();
                
                accumulatedCost = new AccumulatedCostDetails(subscription, 
                    costs, 
                    forecastedCosts, 
                    Enumerable.Empty<CostNamedItem>(),
                    Enumerable.Empty<CostNamedItem>(),
                    Enumerable.Empty<CostNamedItem>());


                ctx.Status = "Performing the daily change check...";
                // Perform the daily change check
                result = CheckDailyChange(accumulatedCost, settings.Percentage, settings.FixedAmount);
            });

        return result;
    }

    private ThresholdResult CheckDailyChange(AccumulatedCostDetails details, double? percentageThreshold,
        double? fixedThreshold)
    {
        var date = details.Costs.MaxBy(a => a.Date).Date;
        
        var today = details.Costs.SingleOrDefault(c => c.Date == date);
        var yesterday = details.Costs.SingleOrDefault(c => c.Date == date.AddDays(-1));

        if (today != null && yesterday != null)
        {
            var changePercentage = ((today.Cost - yesterday.Cost) / yesterday.Cost) * 100;
            var changeFixedAmount = today.Cost - yesterday.Cost;

            bool isExceeded =
                (percentageThreshold.HasValue && Math.Abs(changePercentage) >= percentageThreshold.Value) ||
                (fixedThreshold.HasValue && Math.Abs(changeFixedAmount) >= fixedThreshold.Value);

            return new ThresholdResult(
                "daily-change",
                isExceeded,
                changeFixedAmount,
                fixedThreshold ?? percentageThreshold,
                $"Daily cost changed by {changePercentage:F2}%, which is a fixed amount of {changeFixedAmount:F2} {details.Costs.FirstOrDefault().Currency}."
            );
        }

        return new ThresholdResult("daily-change", false, null, null, "Insufficient data for today or yesterday.");
    }
}