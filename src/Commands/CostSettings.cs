using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands;

public class CostSettings : LogCommandSettings
{
    [CommandOption("-s|--subscription")]
    [Description("The subscription id to use.")]
    public Guid Subscription { get; set; }

    [CommandOption("-o|--output")] 
    [Description("The output format to use. Defaults to Console (Console, Json, JsonC, Text, Markdown)")]
    public OutputFormat Output { get; set; } = OutputFormat.Console;
    
    [CommandOption("-t|--timeframe")]
    [Description(  "The timeframe to use for the costs. Defaults to BillingMonthToDate. When set to Custom, specify the from and to dates using the --from and --to options")]
    public TimeframeType Timeframe { get; set; } = TimeframeType.BillingMonthToDate;
    
    [CommandOption("--from")]
    [Description("The start date to use for the costs. Defaults to the first day of the previous month.")]
    public DateOnly From { get; set; } = DateOnly.FromDateTime( new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1));
    
    [CommandOption("--to")]
    [Description("The end date to use for the costs. Defaults to the current date.")]
    public DateOnly To { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    
    [CommandOption("--others-cutoff")]
    [Description("The number of items to show before collapsing the rest into an 'Others' item.")]
    [DefaultValue(10)]
    public int OthersCutoff { get; set; } = 10;
    
    [CommandOption("--query")]
    [Description("JMESPath query string. See http://jmespath.org/ for more information and examples.")]
    public string Query { get; set; } = string.Empty;
}