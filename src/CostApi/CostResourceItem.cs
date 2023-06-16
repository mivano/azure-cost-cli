namespace AzureCostCli.CostApi;

public record CostResourceItem(double Cost, double CostUSD, string ResourceId, string ResourceType, Guid SubscriptionId,
    string ResourceLocation, string ChargeType, string ResourceGroupName, string PublisherType, string 
        ServiceName, string ServiceTier, string Meter, Dictionary<string, string> Tags, string Currency);