using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureCostCli.CostApi;

public class UsageDetail
{
    [JsonPropertyName("etag")]
    public string? Etag { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("kind")]
    [Required]
    [JsonConverter(typeof(UsageDetailsKindConverter))]
    public UsageDetailsKind Kind { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string> Tags { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

}


public class PricingModelTypeConstantConverter : JsonConverter<PricingModelTypeConstant>
{
    public override PricingModelTypeConstant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string value = reader.GetString();
            if (Enum.TryParse(value, true, out PricingModelTypeConstant pricingModel))
            {
                return pricingModel;
            }
        }

        throw new JsonException($"Unable to convert value '{reader.GetString()}' to {typeof(PricingModelTypeConstant)}.");
    }

    public override void Write(Utf8JsonWriter writer, PricingModelTypeConstant value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}


public class UsageDetailsKindConverter : JsonConverter<UsageDetailsKind>
{
    public override UsageDetailsKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string value = reader.GetString();
            if (Enum.TryParse(value, true, out UsageDetailsKind kind))
            {
                return kind;
            }
        }

        throw new JsonException($"Unable to convert value '{reader.GetString()}' to {typeof(UsageDetailsKind)}.");
    }

    public override void Write(Utf8JsonWriter writer, UsageDetailsKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
