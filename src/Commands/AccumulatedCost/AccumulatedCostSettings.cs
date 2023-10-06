using System.ComponentModel;
using System.Runtime;
using Microsoft.VisualBasic;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.ShowCommand;

public class AccumulatedCostSettings : CostSettings
{
    private Scope _selectedScope;

    public Scope GetScope()
    {
        //_settings = settings;

        if (Subscription == Guid.Empty && Enrollmentaccount != null && BillingAccount != null)
        {
            this._selectedScope = Scope.Enrollment;
        }
        else // default to subscription
        {
            this._selectedScope = Scope.Subscription;
        }


        return this._selectedScope;
    }

    public Uri GetScopeUri()
    {
        // Implement logic to return a different URI based on the selected scope in _settings
        // You can access _settings.SelectedScope to determine the URI
        //string selectedScope = _settings.SelectedScope;

        // logic to determine the URI based on selectedScope
        if (this._selectedScope == Scope.Subscription)
        {
            var uri = new Uri(
                $"/subscriptions/{Subscription}/providers/Microsoft.CostManagement/query?api-version=2021-10-01&$top=5000",
                UriKind.Relative);
            return uri;
        }
        else
        {
            var uri = new Uri(
            $"/providers/Microsoft.Billing/billingAccounts/{BillingAccount}/enrollmentAccounts/{Enrollmentaccount}/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000",
            UriKind.Relative);
            return uri; // Return the determined URI
        }
    }
}

public enum Scope
{
    Subscription,
    Enrollment
}