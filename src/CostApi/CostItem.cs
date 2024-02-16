namespace AzureCostCli.CostApi;

public record CostItem(DateOnly Date, double Cost, double CostUsd, string Currency, string Tags);