namespace AzureCostCli.CostApi;

public interface IRegionsRetriever
{
    Task<IReadOnlyCollection<AzureRegion>> RetrieveRegions();
}