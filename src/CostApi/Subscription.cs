namespace AzureCostCli.CostApi;

public record Subscription(
    string id,
    string authorizationSource,
    object[] managedByTenants,
    string subscriptionId,
    string tenantId,
    string displayName,
    string state,
    SubscriptionPolicies subscriptionPolicies
);

public record SubscriptionPolicies(
    string locationPlacementId,
    string quotaId,
    string spendingLimit
);