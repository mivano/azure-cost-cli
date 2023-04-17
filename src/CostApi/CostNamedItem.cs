namespace AzureCostCli.CostApi;

public record CostNamedItem(string ItemName, double Cost, double CostUsd, string Currency);