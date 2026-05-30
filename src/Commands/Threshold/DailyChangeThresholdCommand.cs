using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Threshold;

/// <summary>
/// Compares today's total cost to yesterday's. Triggers if the change (absolute or %)
/// exceeds the configured threshold.
/// </summary>
public class DailyChangeThresholdCommand : BaseThresholdCommand<ThresholdSettings>
{
    public DailyChangeThresholdCommand(ICostRetriever costRetriever) : base(costRetriever) { }

    protected override async Task<int> ExecuteAsync(CommandContext context, ThresholdSettings settings,
        CancellationToken cancellationToken)
    {
        CommandHelpers.PrintVersionIfDebug(settings.Debug);
        CostRetriever.CostApiAddress = settings.CostApiAddress;
        CostRetriever.HttpTimeout = TimeSpan.FromSeconds(settings.HttpTimeout);

        // Fetch daily costs for the last two days
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        var costs = (await CostRetriever.RetrieveCosts(
            settings.Debug,
            settings.GetScope,
            settings.Filter,
            settings.Metric,
            TimeframeType.Custom,
            yesterday,
            today)).ToList();

        var currency = settings.UseUSD ? "USD" : (costs.FirstOrDefault()?.Currency ?? "USD");

        var todayCost = costs.Where(c => c.Date == today).Sum(c => settings.UseUSD ? c.CostUsd : c.Cost);
        var yesterdayCost = costs.Where(c => c.Date == yesterday).Sum(c => settings.UseUSD ? c.CostUsd : c.Cost);

        double changePct = yesterdayCost == 0
            ? (todayCost > 0 ? 100.0 : 0.0)
            : (todayCost - yesterdayCost) / yesterdayCost * 100.0;
        double changeAbs = todayCost - yesterdayCost;

        bool exceeded = IsExceeded(changePct, changeAbs, settings);

        var message = exceeded
            ? $"Daily cost change exceeds threshold: today={todayCost:N2} {currency}, yesterday={yesterdayCost:N2} {currency}, change={changeAbs:+0.00;-0.00} ({changePct:+0.0;-0.0}%)"
            : $"Daily cost change within threshold: today={todayCost:N2} {currency}, yesterday={yesterdayCost:N2} {currency}, change={changeAbs:+0.00;-0.00} ({changePct:+0.0;-0.0}%)";

        double actualValue = settings.Percentage.HasValue ? changePct : changeAbs;

        var result = new ThresholdResult("daily-change", exceeded, actualValue, settings.Percentage ?? settings.FixedAmount, message);

        await OutputFormatters[settings.Output].WriteThreshold(settings, result);

        return settings.FailOnThreshold && exceeded ? 1 : 0;
    }
}
