using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.CostByResource;

public class CostByResourceCommand : AsyncCommand<CostByResourceSettings>
{
    private readonly ICostRetriever _costRetriever;
    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = OutputFormatterFactory.Create();

    public CostByResourceCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;
    }

    protected override ValidationResult Validate(CommandContext context, CostByResourceSettings settings)
    {
        var subResult = CommandHelpers.ValidateAndResolveSubscription(
            settings.Subscription, settings.GetScope.IsSubscriptionBased,
            id => settings.Subscription = id);
        if (!subResult.Successful) return subResult;

        var timeframeResult = CommandHelpers.ValidateTimeframe(settings);
        if (!timeframeResult.Successful) return timeframeResult;

        return settings.ValidateCostByResourceSettings();
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, CostByResourceSettings settings, CancellationToken cancellationToken)
    {
        CommandHelpers.PrintVersionIfDebug(settings.Debug);

        _costRetriever.CostApiAddress = settings.CostApiAddress;
        _costRetriever.HttpTimeout = TimeSpan.FromSeconds(settings.HttpTimeout);

        IEnumerable<CostResourceItem> resources = Enumerable.Empty<CostResourceItem>();

        await AnsiConsoleExt.StatusAsync(settings.Quiet, "Fetching cost data for resources...", async ctx =>
            {
                resources = await _costRetriever.RetrieveCostForResources(
                    settings.Debug,
                    settings.GetScope, settings.Filter,
                    settings.Metric,
                    settings.ExcludeMeterDetails,
                    settings.Timeframe,
                    settings.GetFromDate(),
                    settings.GetToDate());
            });

        // Sort resources (use USD cost when UseUSD is set so ordering matches displayed amounts)
        resources = settings.Sort.ToLowerInvariant() switch
        {
            "cost-asc" => resources.OrderBy(r => settings.UseUSD ? r.CostUSD : r.Cost),
            "name" => resources.OrderBy(r => r.ResourceId),
            "resource-group" => resources.OrderBy(r => r.ResourceGroupName),
            "resource-type" => resources.OrderBy(r => r.ResourceType),
            "location" => resources.OrderBy(r => r.ResourceLocation),
            _ => resources.OrderByDescending(r => settings.UseUSD ? r.CostUSD : r.Cost) // "cost" and default
        };

        // Materialize to get total count/cost before truncating
        var allResources = resources.ToList();
        var totalCount = allResources.Select(r => r.ResourceId).Distinct().Count();
        var totalCost = allResources.Sum(r => settings.UseUSD ? r.CostUSD : r.Cost);
        var currency = allResources.FirstOrDefault()?.Currency ?? "USD";

        // Apply top filter at the resource level so that all meter-detail rows
        // for a selected resource are kept in the output.
        IEnumerable<CostResourceItem> outputResources = allResources;
        if (settings.Top > 0)
        {
            var topResourceIds = new HashSet<string>(
                allResources
                    .GroupBy(r => r.ResourceId ?? string.Empty)
                    .OrderByDescending(g => g.Sum(r => settings.UseUSD ? r.CostUSD : r.Cost))
                    .Take(settings.Top)
                    .Select(g => g.Key));

            outputResources = allResources.Where(r => topResourceIds.Contains(r.ResourceId ?? string.Empty));
        }

        // Write the output
        await _outputFormatters[settings.Output]
            .WriteCostByResource(settings, outputResources, totalCount, totalCost, currency);

        // Check cost threshold after output
        return CommandHelpers.CheckCostThreshold(totalCost, settings.FailIfOver, currency);
    }
}