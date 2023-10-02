namespace AzureCostCli.Commands.Threshold;

public record ThresholdResult(
    string SubCommand,
    bool IsThresholdExceeded,
    double? ActualValue,
    double? ThresholdValue,
    string AdditionalInfo // Could be used for more detailed textual information
)
{
    public static readonly ThresholdResult Empty = new("", false, null, null, "");
}