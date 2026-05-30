using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Threshold;

/// <summary>
/// Computes the last 7 days average daily cost and triggers if it exceeds the threshold.
/// </summary>
public class WeeklyAverageThresholdCommand : BaseThresholdCommand<ThresholdSettings>
{
    public WeeklyAverageThresholdCommand(ICostRetriever costRetriever) : base(costRetriever) { }

    protected override async Task<int> ExecuteAsync(CommandContext context, ThresholdSettings settings,
        CancellationToken cancellationToken)
    {
        CommandHelpers.PrintVersionIfDebug(settings.Debug);
        CostRetriever.CostApiAddress = settings.CostApiAddress;
        CostRetriever.HttpTimeout = TimeSpan.FromSeconds(settings.HttpTimeout);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sevenDaysAgo = today.AddDays(-6); // inclusive last 7 days

        var costs = (await CostRetriever.RetrieveCosts(
            settings.Debug, settings.GetScope, settings.Filter, settings.Metric,
            TimeframeType.Custom, sevenDaysAgo, today)).ToList();

        var currency = costs.FirstOrDefault()?.Currency ?? "USD";

        var total = costs.Sum(c => settings.UseUSD ? c.CostUsd : c.Cost);
        var average = costs.Count > 0 ? total / 7.0 : 0.0;

        // For weekly-average we compare the average against the fixed-amount threshold directly,
        // or use percentage as a ratio above zero (i.e. any non-zero average triggers if % is 0).
        // Semantics: exceeded if average > fixed-amount, or if percentage threshold is set,
        // we use it as the absolute percentage of average growth vs the threshold value directly.
        bool exceeded = false;
        if (settings.FixedAmount.HasValue && average > settings.FixedAmount.Value)
            exceeded = true;
        if (settings.Percentage.HasValue && average > settings.Percentage.Value)
            exceeded = true;

        var thresholdValue = settings.FixedAmount ?? settings.Percentage;

        var message = exceeded
            ? $"Weekly average daily cost exceeds threshold: average={average:N2} {currency}/day (total={total:N2} {currency} over 7 days), threshold={thresholdValue:N2}"
            : $"Weekly average daily cost within threshold: average={average:N2} {currency}/day (total={total:N2} {currency} over 7 days)";

        var result = new ThresholdResult("weekly-average", exceeded, average, thresholdValue, message);

        await OutputFormatters[settings.Output].WriteThreshold(settings, result);

        return settings.FailOnThreshold && exceeded ? 1 : 0;
    }
}
