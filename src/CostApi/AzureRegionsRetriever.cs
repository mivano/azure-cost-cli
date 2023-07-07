using System.Net.Http.Json;

namespace AzureCostCli.CostApi;

public class AzureRegionsRetriever: IRegionsRetriever
{
    private readonly HttpClient _client;

    public AzureRegionsRetriever(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("RegionsApi");
    }

    public async Task<IReadOnlyCollection<AzureRegion>> RetrieveRegions()
    {
        var uri = new Uri(
            $"globe/data/geo/regions.json",
            UriKind.Relative);

        var response = await _client.GetAsync(uri);

        response.EnsureSuccessStatusCode();

        var regions  = await response.Content.ReadFromJsonAsync < IReadOnlyCollection<AzureRegion>>();

        return regions;
    }
}

public class AzureRegion
{
    public string id { get; set; }
    public string continent { get; set; }
    public string geographyId { get; set; }
    public string displayName { get; set; }
    public string location { get; set; }
    public double latitude { get; set; }
    public double longitude { get; set; }
    public string typeId { get; set; }
    public bool isOpen { get; set; }
    public int? yearOpen { get; set; }
    public string[] complianceIds { get; set; }
    public bool hasGroundStation { get; set; }
    public string dataResidency { get; set; }
    public string availableTo { get; set; }
    public string availabilityZonesId { get; set; }
    public string[] availabilityZonesNearestRegionIds { get; set; }
    public string productsByRegionLink { get; set; }
    public string productsByRegionLinkNonRegional { get; set; }
    public string[] sustainabilityIds { get; set; }
    public string[] disasterRecoveryCrossregionIds { get; set; }
    public string[] disasterRecoveryInregionIds { get; set; }
    
}