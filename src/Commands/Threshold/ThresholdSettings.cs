using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Threshold;

public class ThresholdSettings : CostSettings
{
    [CommandOption("--percentage")]
    [Description("Trigger the threshold if the change or deviation exceeds this percentage. E.g. 20 means 20%.")]
    public double? Percentage { get; set; }

    [CommandOption("--fixed-amount")]
    [Description("Trigger the threshold if the change or deviation exceeds this fixed monetary amount.")]
    public double? FixedAmount { get; set; }

    [CommandOption("--fail-on-threshold")]
    [Description("Return exit code 1 when the threshold is exceeded. Useful for failing CI/CD pipelines. Defaults to false.")]
    [DefaultValue(false)]
    public bool FailOnThreshold { get; set; }
}
