using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Threshold;

/// <summary>
/// Loops through cost-by-service, comparing current period to previous period.
/// Triggers if any single service cost exceeds the threshold.
/// </summary>
public class ServiceSpikeThresholdCommand : BaseThresholdCommand<ThresholdSettings>
{
    public ServiceSpikeThresholdCommand(ICostRetriever costRetriever) : base(costRetriever) { }

    public override async Task<int> ExecuteAsync(CommandContext context, ThresholdSettings settings,
        CancellationToken cancellationToken)
    {
        CommandHelpers.PrintVersionIfDebug(settings.Debug);
        CostRetriever.CostApiAddress = settings.CostApiAddress;
        CostRetriever.HttpTimeout = TimeSpan.FromSeconds(settings.HttpTimeout);

        var to = settings.GetToDate();
        var from = settings.GetFromDate();
        var periodLength = (to.DayNumber - from.DayNumber) + 1;

        // Previous period of same length
        var prevTo = from.AddDays(-1);
        var prevFrom = prevTo.AddDays(-(periodLength - 1));

        var currentServices = (await CostRetriever.RetrieveCostByServiceName(
            settings.Debug, settings.GetScope, settings.Filter, settings.Metric,
            TimeframeType.Custom, from, to)).ToList();

        var previousServices = (await CostRetriever.RetrieveCostByServiceName(
            settings.Debug, settings.GetScope, settings.Filter, settings.Metric,
            TimeframeType.Custom, prevFrom, prevTo)).ToList();

        var currency = settings.UseUSD ? "USD" : (currentServices.FirstOrDefault()?.Currency
                       ?? previousServices.FirstOrDefault()?.Currency
                       ?? "USD");

        // Check every service; exceeded if any service's cost increase breaches the threshold.
        var changes = currentServices.Select(svc =>
        {
            var curr = settings.UseUSD ? svc.CostUsd : svc.Cost;
            var prev = previousServices.FirstOrDefault(p => p.ItemName == svc.ItemName);
            var prevCost = prev == null ? 0 : (settings.UseUSD ? prev.CostUsd : prev.Cost);

            double pct = prevCost == 0
                ? (curr > 0 ? 100.0 : 0.0)
                : (curr - prevCost) / prevCost * 100.0;
            double abs = curr - prevCost;

            return (Service: svc.ItemName, Pct: pct, Abs: abs, Exceeded: IsExceeded(pct, abs, settings));
        }).ToList();

        bool exceeded = changes.Any(c => c.Exceeded);

        // Report the breaching service with the largest cost increase; if none breach, report the
        // service with the largest percentage increase for the OK message.
        var reported = changes.Count == 0
            ? default
            : exceeded
                ? changes.Where(c => c.Exceeded).MaxBy(c => c.Abs)
                : changes.MaxBy(c => c.Pct);

        var spikedService = reported.Service ?? string.Empty;
        double maxChangePct = reported.Pct;
        double maxChangeAbs = reported.Abs;

        double actualValue = settings.Percentage.HasValue ? maxChangePct : maxChangeAbs;

        var message = exceeded
            ? $"Service spike detected for '{spikedService}': change={maxChangeAbs:+0.00;-0.00} {currency} ({maxChangePct:+0.0;-0.0}%)"
            : string.IsNullOrEmpty(spikedService)
                ? "No service cost data found for comparison."
                : $"No service spike detected (max change: '{spikedService}' {maxChangeAbs:+0.00;-0.00} {currency} ({maxChangePct:+0.0;-0.0}%))";

        var result = new ThresholdResult("service-spike", exceeded, actualValue, settings.Percentage ?? settings.FixedAmount, message);

        await OutputFormatters[settings.Output].WriteThreshold(settings, result);

        return settings.FailOnThreshold && exceeded ? 1 : 0;
    }
}
