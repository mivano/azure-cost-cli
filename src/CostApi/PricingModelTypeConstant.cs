using System.ComponentModel;

namespace AzureCostCli.CostApi;

public enum PricingModelTypeConstant
{
    [Description("On Demand")]
    OnDemand,

    [Description("Reservation")]
    Reservation,

    [Description("Spot")]
    Spot,
}