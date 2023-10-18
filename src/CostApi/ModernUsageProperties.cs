using System.Text.Json.Serialization;

namespace AzureCostCli.CostApi;

public class ModernUsageProperties
{
    [JsonPropertyName("additionalInfo")]
    public string? AdditionalInfo { get; set; }

    [JsonPropertyName("benefitId")]
    public string? BenefitId { get; set; }

    [JsonPropertyName("benefitName")]
    public string? BenefitName { get; set; }

    [JsonPropertyName("billingAccountId")]
    public string? BillingAccountId { get; set; }

    [JsonPropertyName("billingAccountName")]
    public string? BillingAccountName { get; set; }

    [JsonPropertyName("billingCurrencyCode")]
    public string? BillingCurrencyCode { get; set; }

    [JsonPropertyName("billingPeriodEndDate")]
    public DateTime? BillingPeriodEndDate { get; set; }

    [JsonPropertyName("billingPeriodStartDate")]
    public DateTime? BillingPeriodStartDate { get; set; }

    [JsonPropertyName("billingProfileId")]
    public string? BillingProfileId { get; set; }

    [JsonPropertyName("billingProfileName")]
    public string? BillingProfileName { get; set; }

    [JsonPropertyName("chargeType")]
    public string? ChargeType { get; set; }

    [JsonPropertyName("consumedService")]
    public string? ConsumedService { get; set; }

    [JsonPropertyName("costAllocationRuleName")]
    public string? CostAllocationRuleName { get; set; }

    [JsonPropertyName("costCenter")]
    public string? CostCenter { get; set; }

    [JsonPropertyName("costInBillingCurrency")]
    public float? CostInBillingCurrency { get; set; }

    [JsonPropertyName("costInPricingCurrency")]
    public float? CostInPricingCurrency { get; set; }

    [JsonPropertyName("costInUSD")]
    public float? CostInUSD { get; set; }

    [JsonPropertyName("customerName")]
    public string? CustomerName { get; set; }

    [JsonPropertyName("customerTenantId")]
    public string? CustomerTenantId { get; set; }

    [JsonPropertyName("date")]
    public DateTime? Date { get; set; }

    [JsonPropertyName("effectivePrice")]
    public float? EffectivePrice { get; set; }

    [JsonPropertyName("exchangeRate")]
    public string? ExchangeRate { get; set; }

    [JsonPropertyName("exchangeRateDate")]
    public DateTime? ExchangeRateDate { get; set; }

    [JsonPropertyName("exchangeRatePricingToBilling")]
    public float? ExchangeRatePricingToBilling { get; set; }

    [JsonPropertyName("frequency")]
    public string? Frequency { get; set; }

    [JsonPropertyName("instanceName")]
    public string? InstanceName { get; set; }

    [JsonPropertyName("invoiceId")]
    public string? InvoiceId { get; set; }

    [JsonPropertyName("invoiceSectionId")]
    public string? InvoiceSectionId { get; set; }

    [JsonPropertyName("invoiceSectionName")]
    public string? InvoiceSectionName { get; set; }

    [JsonPropertyName("isAzureCreditEligible")]
    public bool? IsAzureCreditEligible { get; set; }

    [JsonPropertyName("marketPrice")]
    public float? MarketPrice { get; set; }

    [JsonPropertyName("meterCategory")]
    public string? MeterCategory { get; set; }

    [JsonPropertyName("meterId")]
    public string? MeterId { get; set; }

    [JsonPropertyName("meterName")]
    public string? MeterName { get; set; }

    [JsonPropertyName("meterRegion")]
    public string? MeterRegion { get; set; }

    [JsonPropertyName("meterSubCategory")]
    public string? MeterSubCategory { get; set; }

    [JsonPropertyName("partnerEarnedCreditApplied")]
    public string? PartnerEarnedCreditApplied { get; set; }

    [JsonPropertyName("partnerEarnedCreditRate")]
    public float? PartnerEarnedCreditRate { get; set; }

    [JsonPropertyName("partnerName")]
    public string? PartnerName { get; set; }

    [JsonPropertyName("partnerTenantId")]
    public string? PartnerTenantId { get; set; }

    [JsonPropertyName("payGPrice")]
    public float? PayGPrice { get; set; }

    [JsonPropertyName("paygCostInBillingCurrency")]
    public float? PaygCostInBillingCurrency { get; set; }

    [JsonPropertyName("paygCostInUSD")]
    public float? PaygCostInUSD { get; set; }

    [JsonPropertyName("previousInvoiceId")]
    public string? PreviousInvoiceId { get; set; }

    [JsonPropertyName("pricingCurrencyCode")]
    public string? PricingCurrencyCode { get; set; }

    [JsonPropertyName("pricingModel")]
    [JsonConverter(typeof(PricingModelTypeConstantConverter))]
    public PricingModelTypeConstant? PricingModel { get; set; }

    [JsonPropertyName("product")]
    public string? Product { get; set; }

    [JsonPropertyName("productIdentifier")]
    public string? ProductIdentifier { get; set; }

    [JsonPropertyName("productOrderId")]
    public string? ProductOrderId { get; set; }

    [JsonPropertyName("productOrderName")]
    public string? ProductOrderName { get; set; }

    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("publisherId")]
    public string? PublisherId { get; set; }

    [JsonPropertyName("publisherName")]
    public string? PublisherName { get; set; }

    [JsonPropertyName("publisherType")]
    public string? PublisherType { get; set; }

    [JsonPropertyName("quantity")]
    public float? Quantity { get; set; }

    [JsonPropertyName("resellerMpnId")]
    public string? ResellerMpnId { get; set; }

    [JsonPropertyName("resellerName")]
    public string? ResellerName { get; set; }

    [JsonPropertyName("reservationId")]
    public string? ReservationId { get; set; }

    [JsonPropertyName("reservationName")]
    public string? ReservationName { get; set; }

    [JsonPropertyName("resourceGroup")]
    public string? ResourceGroup { get; set; }

    [JsonPropertyName("resourceLocation")]
    public string? ResourceLocation { get; set; }

    [JsonPropertyName("resourceLocationNormalized")]
    public string? ResourceLocationNormalized { get; set; }

    [JsonPropertyName("serviceFamily")]
    public string? ServiceFamily { get; set; }

    [JsonPropertyName("serviceInfo1")]
    public string? ServiceInfo1 { get; set; }

    [JsonPropertyName("serviceInfo2")]
    public string? ServiceInfo2 { get; set; }

    [JsonPropertyName("servicePeriodEndDate")]
    public DateTime? ServicePeriodEndDate { get; set; }

    [JsonPropertyName("servicePeriodStartDate")]
    public DateTime? ServicePeriodStartDate { get; set; }

    [JsonPropertyName("subscriptionGuid")]
    public string? SubscriptionGuid { get; set; }

    [JsonPropertyName("subscriptionName")]
    public string? SubscriptionName { get; set; }

    [JsonPropertyName("term")]
    public string? Term { get; set; }

    [JsonPropertyName("unitOfMeasure")]
    public string? UnitOfMeasure { get; set; }

    [JsonPropertyName("unitPrice")]
    public float? UnitPrice { get; set; }
}