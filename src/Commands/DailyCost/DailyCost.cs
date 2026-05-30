using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.DailyCost;

public class DailyCostCommand : AsyncCommand<DailyCostSettings>
{
    private readonly ICostRetriever _costRetriever;
    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = OutputFormatterFactory.Create();

    public DailyCostCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;
    }

    protected override ValidationResult Validate(CommandContext context, DailyCostSettings settings)
    {
        var subResult = CommandHelpers.ValidateAndResolveSubscription(
            settings.Subscription, settings.GetScope.IsSubscriptionBased,
            id => settings.Subscription = id);
        if (!subResult.Successful) return subResult;

        return CommandHelpers.ValidateTimeframe(settings);
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, DailyCostSettings settings, CancellationToken cancellationToken)
    {
        CommandHelpers.PrintVersionIfDebug(settings.Debug);

        _costRetriever.CostApiAddress = settings.CostApiAddress;
        _costRetriever.HttpTimeout = TimeSpan.FromSeconds(settings.HttpTimeout);

        IEnumerable<CostDailyItem> dailyCost = Enumerable.Empty<CostDailyItem>();

        // if output format is not csv, json, or jsonc, then don't include tags
        if (settings.Output != OutputFormat.Json &&
            settings.Output != OutputFormat.Jsonc &&
            settings.Output != OutputFormat.Csv)
        {
            settings.IncludeTags = false;
        }

        await AnsiConsoleExt.StatusAsync(settings.Quiet, "Fetching daily cost data...", async ctx =>
            {
                // Fetch the costs from the Azure Cost Management API

                dailyCost = await _costRetriever.RetrieveDailyCost(settings.Debug, settings.GetScope,
                    settings.Filter,
                    settings.Metric,
                    settings.Dimension,
                    settings.Timeframe,
                    settings.GetFromDate(), settings.GetToDate(),
                    settings.IncludeTags);
            });

        // Write the output
        await _outputFormatters[settings.Output]
            .WriteDailyCost(settings, dailyCost);

        // Check cost threshold after output
        var totalCost = dailyCost.Sum(d => settings.UseUSD ? d.CostUsd : d.Cost);
        var currency = settings.UseUSD ? "USD" : dailyCost.FirstOrDefault()?.Currency ?? "USD";
        return CommandHelpers.CheckCostThreshold(totalCost, settings.FailIfOver, currency);
    }
}