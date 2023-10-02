namespace AzureCostCli.Commands;

public interface ICostSettings
{
    Guid Subscription { get; set; }
    OutputFormat Output { get; set; }
    TimeframeType Timeframe { get; set; }
    DateOnly From { get; set; }
    DateOnly To { get; set; }
    string Query { get; set; }
    bool UseUSD { get; set; }
    bool SkipHeader { get; set; }
    string[] Filter { get; set; }
    MetricType Metric { get; set; }
    bool Debug { get; set; }
}