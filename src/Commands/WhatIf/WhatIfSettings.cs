using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.WhatIf;

public class WhatIfSettings : CommandSettings, ICostSettings //:CostSettings
{
    [CommandOption("--debug")]
    [Description("Increase logging verbosity to show all debug logs.")]
    [DefaultValue(false)]
    public bool Debug { get; set; }
    
    [CommandOption("-s|--subscription")]
    [Description("The subscription id to use. Will try to fetch the active id if not specified.")]
    public Guid? Subscription { get; set; }

    [CommandOption("-g|--resource-group")]
    [Description("The resource group to scope the request to. Need to be used in combination with the subscription id.")]
    public string? ResourceGroup { get; set; }

    [CommandOption("-b|--billing-account")]
    [Description("The billing account id to use.")]
    public string? BillingAccountId { get; set; }
    
    [CommandOption("-e|--enrollment-account")]
    [Description("The enrollment account id to use.")]
    public int? EnrollmentAccountId { get; set; }

    [CommandOption("-o|--output")] 
    [Description("The output format to use. Defaults to Console (Console, Json, JsonC, Text, Markdown, Csv)")]
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
    [Description("JMESPath query string, applicable for the Json output only. See http://jmespath.org/ for more information and examples.")]
    public string Query { get; set; } = string.Empty;
    
    [CommandOption("--useUSD")]
    [Description("Force the use of USD for the currency. Defaults to false to use the currency returned by the API.")]
    [DefaultValue(false)]
    public bool UseUSD { get; set; }
    
    [CommandOption("--skipHeader")]
    [Description("Skip header creation for specific output formats. Useful when appending the output from multiple runs into one file. Defaults to false.")]
    [DefaultValue(false)]
    public bool SkipHeader { get; set; }

    [CommandOption("--filter")]
    [Description("Filter the output by the specified properties. Defaults to no filtering and can be multiple values.")]
    public string[] Filter { get; set; }

    [CommandOption("-m|--metric")]
    [Description("The metric to use for the costs. Defaults to ActualCost. (ActualCost, AmortizedCost)")]
    [DefaultValue(MetricType.ActualCost)]
    public MetricType Metric { get; set; } = MetricType.ActualCost;
    
    [CommandOption("--costApiBaseAddress <BASE_ADDRESS>")]
    [Description("The base address for the Cost API. Defaults to https://management.azure.com/")]
    public string CostApiAddress { get; set; } = "https://management.azure.com/";
    
    [CommandOption("--priceApiBaseAddress <BASE_ADDRESS>")]
    [Description("The base address for the Price API. Defaults to https://prices.azure.com/")]
    public string PriceApiAddress { get; set; } = "https://prices.azure.com/";
    
    public Scope GetScope
    {
        get {
            if ((Subscription==null || Subscription == Guid.Empty) && EnrollmentAccountId != null && BillingAccountId != null)
            {
                return Scope.EnrollmentAccount(BillingAccountId, EnrollmentAccountId.Value);
            }
            else if (Subscription != null && !string.IsNullOrWhiteSpace(ResourceGroup))
            {
                return Scope.ResourceGroup(Subscription.Value, ResourceGroup);
            }
            else if (BillingAccountId != null)
            {
                return Scope.BillingAccount(BillingAccountId);
            }
            else // default to subscription
            {
                return Scope.Subscription(Subscription.GetValueOrDefault(Guid.Empty));
            }
        }
    }
}