namespace AzureCostCli.CostApi;

public record EnrollmentAccount(
    string id,
    string name,
    properties properties
);

public record properties(
    string accountName,
    string costCenter,
    string displayName
 );