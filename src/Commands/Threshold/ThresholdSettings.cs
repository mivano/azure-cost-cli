using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Threshold;

public class ThresholdSettings : CostSettings
{
    [CommandOption("--percentage")]
    [Description("The percentage by which the cost should differ to trigger an alert.")]
    public double? Percentage { get; set; }

    [CommandOption("--fixed-amount")]
    [Description("The fixed amount that would trigger an alert.")]
    public double? FixedAmount { get; set; }

    [CommandOption("--fail-on-error")]
    [Description("Fail the application if the threshold is exceeded.")]
    public bool FailOnError { get; set; } = false;
    
    
}