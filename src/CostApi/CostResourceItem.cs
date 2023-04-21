namespace AzureCostCli.CostApi;

public record CostResourceItem(double Cost, double CostUSD, string ResourceId, string ResourceType,
    string ResourceLocation, string ChargeType, string ResourceGroupName, string PublisherType, string 
        ServiceName, string ServiceTier, string Meter, string[] Tags, string Currency);