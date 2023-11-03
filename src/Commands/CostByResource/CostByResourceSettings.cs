using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.CostByResource;

public class CostByResourceSettings : CostSettings
{
    [CommandOption("--exclude-meter-details")]
    [Description("Exclude meter details from the output.")]
    [DefaultValue(false)]
    public bool ExcludeMeterDetails { get; set; }
}