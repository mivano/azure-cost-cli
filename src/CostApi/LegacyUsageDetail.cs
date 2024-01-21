using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AzureCostCli.CostApi;

public class LegacyUsageDetail: UsageDetail
{
    [JsonPropertyName("properties")]
    [Required]
    public LegacyUsageDetailProperties Properties { get; set; }
}