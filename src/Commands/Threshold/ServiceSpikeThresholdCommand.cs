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

    protected override async Task<int> ExecuteAsync(CommandContext context, ThresholdSettings settings,
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

        // Check every service; exceeded if any service breaches the threshold.
        // Among all breaching services (or all services if none breach) track the worst by abs change.
        var spikedService = string.Empty;
        double maxChangePct = 0;
        double maxChangeAbs = 0;
        bool exceeded = false;

        foreach (var svc in currentServices)
        {
            var curr = settings.UseUSD ? svc.CostUsd : svc.Cost;
            var prev = previousServices.FirstOrDefault(p => p.ItemName == svc.ItemName);
            var prevCost = prev == null ? 0 : (settings.UseUSD ? prev.CostUsd : prev.Cost);

            double pct = prevCost == 0
                ? (curr > 0 ? 100.0 : 0.0)
                : (curr - prevCost) / prevCost * 100.0;
            double abs = curr - prevCost;

            bool svcExceeded = IsExceeded(pct, abs, settings);
            if (svcExceeded) exceeded = true;

            // Report the service with the largest absolute change among all that exceeded;
            // if none exceeded yet, track the worst overall so we can report it in the OK message.
            if (svcExceeded && Math.Abs(abs) > Math.Abs(maxChangeAbs) ||
                !exceeded && Math.Abs(pct) > Math.Abs(maxChangePct))
            {
                maxChangePct = pct;
                maxChangeAbs = abs;
                spikedService = svc.ItemName;
            }
        }

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
