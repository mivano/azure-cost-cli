using System.Text.Json;

namespace AzureCostCli.CostApi;

public class QueryResponse
{
    public object eTag { get; set; }
    public string id { get; set; }
    public object location { get; set; }
    public string name { get; set; }
    public Properties properties { get; set; }
    public string type { get; set; }
    
    // Combine method to merge results
    public void Combine(QueryResponse other)
    {
        if (other?.properties?.rows != null)
        {
            this.properties.rows.AddRange(other.properties.rows);
        }
    }
}

public class Columns
{
    public string name { get; set; }
    public string type { get; set; }
}

public class Properties
{
    public Columns[] columns { get; set; }
    public string nextLink { get; set; }
    public List<JsonElement> rows { get; set; }
}