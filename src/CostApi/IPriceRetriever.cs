namespace AzureCostCli.CostApi;

public interface IPriceRetriever
{
    Task<IEnumerable<PriceRecord>> GetAzurePricesAsync(string? filter = null);
}