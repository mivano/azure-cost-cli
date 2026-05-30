namespace AzureCostCli.Commands.Threshold;

public record ThresholdResult(
    string SubCommand,
    bool IsThresholdExceeded,
    double? ActualValue,
    double? ThresholdValue,
    string Message);
