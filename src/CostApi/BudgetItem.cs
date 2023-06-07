namespace AzureCostCli.CostApi;


public record BudgetItem(string Name, string Id, double Amount, string TimeGrain, DateTime StartDate, DateTime EndDate, double? CurrentSpendAmount, string CurrentSpendCurrency, double? ForecastAmount, string ForecastCurrency, List<Notification> Notifications);

public record Notification(string Name, bool Enabled, string Operator, double Threshold, List<string> ContactEmails, List<string> ContactRoles, List<string> ContactGroups);
