using AzureCostCli.Commands;

namespace AzureCostCli.CostApi;

public interface ICostRetriever
{
    Task<Subscription> RetrieveSubscription(bool includeDebugOutput, Guid subscriptionId);
    
    Task<IEnumerable<CostItem>> RetrieveCosts(bool includeDebugOutput, Guid subscriptionId,
        string[] filter,
        TimeframeType timeFrame, DateOnly from, DateOnly to);

    Task<IEnumerable<CostNamedItem>> RetrieveCostByServiceName(bool includeDebugOutput,
        Guid subscriptionId, string[] filter, TimeframeType timeFrame, DateOnly from, DateOnly to);

    Task<IEnumerable<CostNamedItem>> RetrieveCostByLocation(bool includeDebugOutput, Guid subscriptionId,
        string[] filter,
        TimeframeType timeFrame, DateOnly from, DateOnly to);

    Task<IEnumerable<CostNamedItem>> RetrieveCostByResourceGroup(bool includeDebugOutput, Guid subscriptionId,
        string[] filter,
        TimeframeType timeFrame, DateOnly from, DateOnly to);

    Task<IEnumerable<CostItem>> RetrieveForecastedCosts(bool includeDebugOutput, Guid subscriptionId, 
        string[] filter,
        TimeframeType timeFrame, DateOnly from, DateOnly to);
    Task<IEnumerable<CostResourceItem>> RetrieveCostForResources(bool settingsDebug, Guid subscriptionId, string[] filter,TimeframeType settingsTimeframe, DateOnly from, DateOnly to);
    Task<IEnumerable<BudgetItem>> RetrieveBudgets(bool settingsDebug, Guid subscriptionId);
    Task<IEnumerable<CostDailyItem>> RetrieveDailyCost(bool settingsDebug, Guid subscriptionId, string[] filter, string dimension, TimeframeType settingsTimeframe, DateOnly settingsFrom, DateOnly settingsTo);
}