using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureCostCli.CostApi;

public class AzurePriceRetriever : IPriceRetriever
{
    private readonly HttpClient _client;
    public string PriceApiAddress { get; set; }
    
    public AzurePriceRetriever(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("PriceApi");
    }

    public async Task<IEnumerable<PriceRecord>> GetAzurePricesAsync(string currencyCode = "USD", string? filter = null)
    {
        if (!string.Equals(_client.BaseAddress?.ToString(), PriceApiAddress))
        {
            _client.BaseAddress = new Uri(PriceApiAddress);
        }

        var prices = new List<PriceRecord>();
        string? url = "api/retail/prices?api-version=2023-01-01-preview&currencyCode='" + currencyCode + "'";

        // Append the filter to the URL if it's provided
        if (!string.IsNullOrWhiteSpace(filter))
        {
            url += "&$filter=" + filter;
        }
        
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        while (url != null)
        {
            var response = await _client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<PriceData>(content, options);

                if (data.Items != null)
                {
                    prices.AddRange(data.Items);
                }

                url = data.NextPageLink; // get the next page link
            }
            else
            {
                throw new Exception($"Failed to get data from API. Status code: {response.StatusCode}");
            }
        }

        return prices;
    }

   
}

public class PriceRecord
{
    public string CurrencyCode { get; set; }
    public double TierMinimumUnits { get; set; }
    public double RetailPrice { get; set; }
    public double UnitPrice { get; set; }
    public string ArmRegionName { get; set; }
    public string Location { get; set; }
    public DateTime EffectiveStartDate { get; set; }
    public string MeterId { get; set; }
    public string MeterName { get; set; }
    public string ProductId { get; set; }
    public string SkuId { get; set; }
    public string ProductName { get; set; }
    public string SkuName { get; set; }
    public string ServiceName { get; set; }
    public string ServiceId { get; set; }
    public string ServiceFamily { get; set; }
    public string UnitOfMeasure { get; set; }
    public string Type { get; set; }
    public bool IsPrimaryMeterRegion { get; set; }
    public string ArmSkuName { get; set; }
    public List<SavingsPlan> SavingsPlan { get; set; }
    public string ReservationTerm { get; set; } // Only present in some records

    public string Sku
    {
        get
        {
            var sku = $"{SkuId}/{MeterId}";

            if (ServiceName is "Virtual Machines" or "Azure App Service") {
                sku = $"{ProductId}/{ArmSkuName}/{MeterId}";
            }
            
             return sku;
        }
    }

}

public class SavingsPlan
{
    public double UnitPrice { get; set; }
    public double RetailPrice { get; set; }
    public string Term { get; set; }
}


    public class PriceData
    {
        [JsonPropertyName("Items")]
        public List<PriceRecord> Items { get; set; }

        [JsonPropertyName("NextPageLink")]
        public string? NextPageLink { get; set; }
    }
