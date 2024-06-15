using AzureCostCli.Commands;

namespace AzureCostCli.CostApi;

public interface ICostRetriever
{
    public string CostApiAddress { get; set; }
    public TimeSpan HttpTimeout { get; set; }

    Task<Subscription> RetrieveSubscription(bool includeDebugOutput, Guid subscriptionId);
    
    Task<IEnumerable<CostItem>> RetrieveCosts(bool includeDebugOutput, Scope scope,
        string[] filter,MetricType metric,
        TimeframeType timeFrame, DateOnly from, DateOnly to);

    Task<IEnumerable<CostNamedItem>> RetrieveCostByServiceName(bool includeDebugOutput,
        Scope scope, string[] filter, MetricType metric,TimeframeType timeFrame, DateOnly from, DateOnly to);

    Task<IEnumerable<CostNamedItem>> RetrieveCostByLocation(bool includeDebugOutput, Scope scope,
        string[] filter,MetricType metric,
        TimeframeType timeFrame, DateOnly from, DateOnly to);

    Task<IEnumerable<CostNamedItem>> RetrieveCostByResourceGroup(bool includeDebugOutput, Scope scope,
        string[] filter,MetricType metric,
        TimeframeType timeFrame, DateOnly from, DateOnly to);

    Task<IEnumerable<CostNamedItem>> RetrieveCostBySubscription(bool includeDebugOutput, Scope scope,
        string[] filter, MetricType metric,
        TimeframeType timeFrame, DateOnly from, DateOnly to);

    Task<IEnumerable<CostItem>> RetrieveForecastedCosts(bool includeDebugOutput, Scope scope, 
        string[] filter,MetricType metric,
        TimeframeType timeFrame, DateOnly from, DateOnly to);
    Task<IEnumerable<CostResourceItem>> RetrieveCostForResources(bool settingsDebug, Scope scope, string[] filter, MetricType metric,
        bool excludeMeterDetails,TimeframeType settingsTimeframe, DateOnly from, DateOnly to);
    Task<IEnumerable<BudgetItem>> RetrieveBudgets(bool settingsDebug, Scope scope);

    Task<IEnumerable<UsageDetails>> RetrieveUsageDetails(bool includeDebugOutput,
        Scope scope, string filter, DateOnly from, DateOnly to);
    
    Task<IEnumerable<CostDailyItem>> RetrieveDailyCost(bool settingsDebug, Scope scope, string[] filter,MetricType metric, string dimension, TimeframeType settingsTimeframe, DateOnly settingsFrom, DateOnly settingsTo, bool includeTags);
}