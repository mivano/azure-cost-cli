using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AzureCostCli.CostApi;

public class ModernUsageDetail: UsageDetail
{
    [JsonPropertyName("properties")]
    [Required]
    public ModernUsageProperties Properties { get; set; }
}