using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.CostByTag;

public class CostByTagSettings : CostSettings
{
    
    [CommandOption("--tag")]
    [Description("The tags to return, for example: Cost Center or Owner. You can specify multiple tags by using the --tag option multiple times.")]
    public string[] Tags { get; set; } = Array.Empty<string>();
}