namespace AzureCostCli.CostApi;

public class UsageDetailsResponse
{
    public UsageDetail[] value { get; set; }
    public string? nextLink { get; set; }
}