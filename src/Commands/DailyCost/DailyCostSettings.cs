using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.ShowCommand;

public class DailyCostSettings : CostSettings
{
    [CommandOption("--dimension")]
    [Description("The grouping to use. E.g. ResourceGroupName, Meter, ResourceLocation, etc. Defaults to ResourceGroupName.")]
    [DefaultValue("ResourceGroupName")]

    public string Dimension { get; set; } = "ResourceGroupName";

   
}