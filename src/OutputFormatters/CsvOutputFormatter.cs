using System.Dynamic;
using System.Globalization;
using AzureCostCli.CostApi;
using CsvHelper;
using CsvHelper.Configuration;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public class CsvOutputFormatter : BaseOutputFormatter
{
    public override Task WriteAccumulatedCost(AccumulatedCostSettings settings, AccumulatedCostDetails accumulatedCostDetails)
    {
        return ExportToCsv(settings.SkipHeader, accumulatedCostDetails.Costs);
    }

    public override Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources)
    {
        return ExportToCsv(settings.SkipHeader, resources);
    }
    
    public override Task WriteBudgets(BudgetsSettings settings, IEnumerable<BudgetItem> budgets)
    {
        return ExportToCsv(settings.SkipHeader, budgets);
    }

    public override Task WriteDailyCost(DailyCostSettings settings, IEnumerable<CostDailyItem> dailyCosts)
    {
        return ExportToCsv(settings.SkipHeader, dailyCosts);
    }

    private static Task ExportToCsv(bool skipHeader, IEnumerable<object> resources)
    {
        var config = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            HasHeaderRecord = skipHeader == false
        };
        using (var writer = new StringWriter())
        using (var csv = new CsvWriter(writer, config))
        {
            csv.WriteRecords(resources);

            Console.Write(writer.ToString());
        }

        return Task.CompletedTask;
    }

   
    
    
}