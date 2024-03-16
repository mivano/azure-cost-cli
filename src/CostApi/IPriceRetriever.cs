namespace AzureCostCli.CostApi;

public interface IPriceRetriever
{
    Task<IEnumerable<PriceRecord>> GetAzurePricesAsync(string currencyCode = "USD", string? filter = null);
    string PriceApiAddress { get; set; }
}