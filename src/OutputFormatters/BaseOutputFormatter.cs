using AzureCostCli.CostApi;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public abstract class BaseOutputFormatter
{
    public abstract Task WriteAccumulatedCost(AccumulatedCostSettings settings,
        IEnumerable<CostItem> costs,
        IEnumerable<CostItem> forecastedCosts,
        IEnumerable<CostNamedItem> byServiceNameCosts,
        IEnumerable<CostNamedItem> byLocationCosts,
        IEnumerable<CostNamedItem> byResourceGroupCosts);

    public abstract Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources);
}