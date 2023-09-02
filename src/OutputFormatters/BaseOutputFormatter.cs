using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.Commands.Budgets;
using AzureCostCli.Commands.CostByResource;
using AzureCostCli.Commands.CostByTag;
using AzureCostCli.Commands.DailyCost;
using AzureCostCli.Commands.DetectAnomaly;
using AzureCostCli.Commands.Regions;
using AzureCostCli.Commands.Threshold;
using AzureCostCli.CostApi;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public abstract class BaseOutputFormatter
{
    public abstract Task WriteAccumulatedCost(AccumulatedCostSettings settings,AccumulatedCostDetails accumulatedCostDetails);

    public abstract Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources);
    
    public abstract Task WriteBudgets(BudgetsSettings settings, IEnumerable<BudgetItem> budgets);

    public abstract Task WriteDailyCost(DailyCostSettings settings, IEnumerable<CostDailyItem> dailyCosts);
    public abstract Task WriteAnomalyDetectionResults(DetectAnomalySettings settings, List<AnomalyDetectionResult> anomalies);
    public abstract Task WriteRegions(RegionsSettings settings, IReadOnlyCollection<AzureRegion> regions);
    public abstract Task WriteCostByTag(CostByTagSettings settings, Dictionary<string, Dictionary<string, List<CostResourceItem>>> byTags);
    public abstract Task WriteThreshold(ThresholdSettings settings, ThresholdResult result);
}

public record AccumulatedCostDetails( 
    Subscription Subscription,
    IEnumerable<CostItem> Costs,
    IEnumerable<CostItem> ForecastedCosts,
    IEnumerable<CostNamedItem> ByServiceNameCosts,
    IEnumerable<CostNamedItem> ByLocationCosts,
    IEnumerable<CostNamedItem> ByResourceGroupCosts);