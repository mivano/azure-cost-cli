using System.ComponentModel;

namespace AzureCostCli.CostApi;

public enum UsageDetailsKind
{
    [Description("legacy")]
    Legacy,

    [Description("modern")]
    Modern,
}