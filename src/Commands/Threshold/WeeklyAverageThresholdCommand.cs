using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Threshold;

/// <summary>
/// Computes the last 7 days average daily cost and triggers if it exceeds the threshold.
/// </summary>
public class WeeklyAverageThresholdCommand : BaseThresholdCommand<ThresholdSettings>
{
    public WeeklyAverageThresholdCommand(ICostRetriever costRetriever) : base(costRetriever) { }

    protected override ValidationResult Validate(CommandContext context, ThresholdSettings settings)
    {
        if (settings.Percentage.HasValue)
            return ValidationResult.Error(
                "The weekly-average command does not support --percentage. " +
                "Use --fixed-amount to specify a monetary threshold.");
        return base.Validate(context, settings);
    }

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

        var currency = settings.UseUSD ? "USD" : (costs.FirstOrDefault()?.Currency ?? "USD");

        var total = costs.Sum(c => settings.UseUSD ? c.CostUsd : c.Cost);
        var average = costs.Count > 0 ? total / 7.0 : 0.0;

        // Only --fixed-amount is supported; --percentage is rejected in Validate.
        bool exceeded = settings.FixedAmount.HasValue && average > settings.FixedAmount.Value;

        var thresholdValue = settings.FixedAmount ?? settings.Percentage;

        var message = exceeded
            ? $"Weekly average daily cost exceeds threshold: average={average:N2} {currency}/day (total={total:N2} {currency} over 7 days), threshold={thresholdValue:N2}"
            : $"Weekly average daily cost within threshold: average={average:N2} {currency}/day (total={total:N2} {currency} over 7 days)";

        var result = new ThresholdResult("weekly-average", exceeded, average, thresholdValue, message);

        await OutputFormatters[settings.Output].WriteThreshold(settings, result);

        return settings.FailOnThreshold && exceeded ? 1 : 0;
    }
}
