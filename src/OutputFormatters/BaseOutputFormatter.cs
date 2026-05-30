using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.Commands.Budgets;
using AzureCostCli.Commands.CostByResource;
using AzureCostCli.Commands.CostByTag;
using AzureCostCli.Commands.DailyCost;
using AzureCostCli.Commands.DetectAnomaly;
using AzureCostCli.Commands.Diff;
using AzureCostCli.Commands.Regions;
using AzureCostCli.Commands.Threshold;
using AzureCostCli.Commands.WhatIf;
using AzureCostCli.CostApi;

namespace AzureCostCli.OutputFormatters;

public abstract class BaseOutputFormatter
{
    public abstract Task WriteAccumulatedCost(AccumulatedCostSettings settings,AccumulatedCostDetails accumulatedCostDetails);

    public abstract Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources,
        int totalCount = 0, double totalCost = 0, string currency = "USD");
    
    public abstract Task WriteBudgets(BudgetsSettings settings, IEnumerable<BudgetItem> budgets);

    public abstract Task WriteDailyCost(DailyCostSettings settings, IEnumerable<CostDailyItem> dailyCosts);
    public abstract Task WriteAnomalyDetectionResults(DetectAnomalySettings settings, List<AnomalyDetectionResult> anomalies);
    public abstract Task WriteRegions(RegionsSettings settings, IReadOnlyCollection<AzureRegion> regions);
    public abstract Task WriteCostByTag(CostByTagSettings settings, Dictionary<string, Dictionary<string, List<CostResourceItem>>> byTags);
    public abstract Task WritePricesPerRegion(WhatIfSettings settings, Dictionary<UsageDetails,List<PriceRecord>> pricesByRegion);
    public abstract Task WriteDevTestComparison(WhatIfSettings settings, IEnumerable<DevTestComparisonItem> items);
    public abstract Task WriteAccumulatedDiffCost(DiffSettings settings, AccumulatedCostDetails accumulatedCostSource,
        AccumulatedCostDetails accumulatedCostTarget);

    public abstract Task WriteThreshold(ThresholdSettings settings, ThresholdResult result);

}

public record DevTestComparisonItem(
    string ResourceName,
    string ResourceGroup,
    string Product,
    string MeterName,
    string Region,
    string Currency,
    string UnitOfMeasure,
    double Quantity,
    double CurrentUnitPrice,
    double CurrentCost,
    double? DevTestUnitPrice,
    double? DevTestCost,
    double? Savings,
    double? SavingsPercentage);

public record AccumulatedCostDetails( 
    Subscription? Subscription,
    EnrollmentAccount? EnrollmentAccount,
    IEnumerable<CostItem> Costs,
    IEnumerable<CostItem> ForecastedCosts,
    IEnumerable<CostNamedItem> ByServiceNameCosts,
    IEnumerable<CostNamedItem> ByLocationCosts,
    IEnumerable<CostNamedItem> ByResourceGroupCosts,
    IEnumerable<CostNamedItem>? BySubscriptionCosts);