using System.Text.Json;

namespace AzureCostCli.CostApi;

public class SecurityCredentials
{
    public SecurityCredentials(string tenantId, string ServicePrincipalId, string ServicePrincipalSecret)
    {
        this.tenantId = tenantId;
        this.ServicePrincipalId = ServicePrincipalId;
        this.ServicePrincipalSecret = ServicePrincipalSecret;
    }
    public string tenantId { get; set; }
    public string ServicePrincipalId { get; set; }
    public string ServicePrincipalSecret { get; set; }
}

public class CostQueryResponse
{
    public object eTag { get; set; }
    public string id { get; set; }
    public object location { get; set; }
    public string name { get; set; }
    public Properties properties { get; set; }
    public string type { get; set; }
}

public class Columns
{
    public string name { get; set; }
    public string type { get; set; }
}

public class Properties
{
    public Columns[] columns { get; set; }
    public object nextLink { get; set; }
    public JsonElement[] rows { get; set; }
}