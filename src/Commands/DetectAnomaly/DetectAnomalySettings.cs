using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.ShowCommand;

public class DetectAnomalySettings : CostSettings
{
    [CommandOption("--dimension")]
    [Description("The grouping to use. E.g. ResourceGroupName, Meter, ResourceLocation, etc. Defaults to ResourceGroupName.")]
    [DefaultValue("ResourceGroupName")]

    public string Dimension { get; set; } = "ResourceGroupName";

    [CommandOption("--recent-activity-days")]
    [Description("The number of days to use for recent activity. Defaults to 7.")]
    [DefaultValue(7)]
    public int RecentActivityDays { get; set; }
    
    [CommandOption("--significant-change")]
    [Description("The significant change in cost to use. Defaults to 0.75 (75%).")]
    [DefaultValue(0.75)]
    public double SignificantChange { get; set; }
    
    [CommandOption("--steady-growth-days")]
    [Description("The number of days to use for steady growth. Defaults to 7.")]
    [DefaultValue(7)]
    public int SteadyGrowthDays { get; set; }
}