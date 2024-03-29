namespace AzureCostCli.CostApi;

public record CostDailyItem(DateOnly Date, string Name, double Cost, double CostUsd, string Currency, Dictionary<string, string>? Tags);
public record CostDailyItemWithoutTags(DateOnly Date, string Name, double Cost, double CostUsd, string Currency);
