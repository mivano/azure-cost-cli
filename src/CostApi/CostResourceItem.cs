namespace AzureCostCli.CostApi;

public record CostResourceItem(double Cost, double CostUSD, string ResourceId, string ResourceType,
    string ResourceLocation, string ChargeType, string ResourceGroupName, string PublisherType, string? 
        ServiceName, string? ServiceTier, string? Meter, Dictionary<string, string> Tags, string Currency);

public static class CostResourceItemExtensions
{
    // Function to extract the name of the resource from the resource id
    public static string GetResourceName(this CostResourceItem resource)
    {
        var parts = resource.ResourceId.Split('/');
        return parts.Last();
    }
}