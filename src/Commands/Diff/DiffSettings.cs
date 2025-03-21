using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Diff;

public class DiffSettings : CostSettings
{
    [CommandOption("--compare-to")]
    [Description("The JSON base file to compare the current costs to.")]
    public string CompareTo { get; set; }
    
    [CommandOption("--compare-from")]
    [Description("The JSON base file to compare the current costs from.")]
    public string CompareFrom { get; set; }
}