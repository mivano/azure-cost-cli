using AzureCostCli.Commands.ShowCommand;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureCostCli.Infrastructure;

public class ScopeSelector
{
    private readonly AccumulatedCostSettings _settings;
    private Scope _selectedScope;

    public ScopeSelector(AccumulatedCostSettings settings)
    {
        _settings = settings;

        if (_settings.Subscription == Guid.Empty)
        {
            this._selectedScope = Scope.Enrollment
        }
        else
        {
            this._selectedScope = Scope.Subscription;
        }
    }

    public Scope GetSelectedScope()
    {
        return this._selectedScope;
    }

    public Uri GetScopeUri()
    {
        // Implement logic to return a different URI based on the selected scope in _settings
        // You can access _settings.SelectedScope to determine the URI
        //string selectedScope = _settings.SelectedScope;

        // Your logic to determine the URI based on selectedScope
        // ...
        var uri = new Uri(
        $"/providers/Microsoft.Billing/billingAccounts/55795860/enrollmentAccounts/303639/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000",
        UriKind.Relative);

        return uri; // Return the determined URI
    }
}

public enum Scope
{
    Subscription,
    Enrollment
}
