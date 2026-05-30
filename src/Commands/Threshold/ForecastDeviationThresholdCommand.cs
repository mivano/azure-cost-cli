using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Threshold;

/// <summary>
/// Compares actual spend to forecasted spend. Triggers if actual deviates from forecast
/// by more than the configured threshold.
/// </summary>
public class ForecastDeviationThresholdCommand : BaseThresholdCommand<ThresholdSettings>
{
    public ForecastDeviationThresholdCommand(ICostRetriever costRetriever) : base(costRetriever) { }

    protected override async Task<int> ExecuteAsync(CommandContext context, ThresholdSettings settings,
        CancellationToken cancellationToken)
    {
        CommandHelpers.PrintVersionIfDebug(settings.Debug);
        CostRetriever.CostApiAddress = settings.CostApiAddress;
        CostRetriever.HttpTimeout = TimeSpan.FromSeconds(settings.HttpTimeout);

        var from = settings.GetFromDate();
        var to = settings.GetToDate();

        var actualCosts = (await CostRetriever.RetrieveCosts(
            settings.Debug, settings.GetScope, settings.Filter, settings.Metric,
            settings.Timeframe, from, to)).ToList();

        var forecastedCosts = (await CostRetriever.RetrieveForecastedCosts(
            settings.Debug, settings.GetScope, settings.Filter, settings.Metric,
            settings.Timeframe, from, to)).ToList();

        var currency = settings.UseUSD ? "USD" : (actualCosts.FirstOrDefault()?.Currency
                       ?? forecastedCosts.FirstOrDefault()?.Currency
                       ?? "USD");

        var actual = actualCosts.Sum(c => settings.UseUSD ? c.CostUsd : c.Cost);
        var forecast = forecastedCosts.Sum(c => settings.UseUSD ? c.CostUsd : c.Cost);

        double deviationPct = forecast == 0
            ? (actual > 0 ? 100.0 : 0.0)
            : (actual - forecast) / forecast * 100.0;
        double deviationAbs = actual - forecast;

        bool exceeded = IsExceeded(deviationPct, deviationAbs, settings);

        var message = exceeded
            ? $"Forecast deviation exceeds threshold: actual={actual:N2} {currency}, forecast={forecast:N2} {currency}, deviation={deviationAbs:+0.00;-0.00} ({deviationPct:+0.0;-0.0}%)"
            : $"Forecast deviation within threshold: actual={actual:N2} {currency}, forecast={forecast:N2} {currency}, deviation={deviationAbs:+0.00;-0.00} ({deviationPct:+0.0;-0.0}%)";

        double actualValue = settings.Percentage.HasValue ? deviationPct : deviationAbs;

        var result = new ThresholdResult("forecast-deviation", exceeded, actualValue, settings.Percentage ?? settings.FixedAmount, message);

        await OutputFormatters[settings.Output].WriteThreshold(settings, result);

        return settings.FailOnThreshold && exceeded ? 1 : 0;
    }
}
