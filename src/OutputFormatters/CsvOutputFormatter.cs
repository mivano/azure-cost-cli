using System.Dynamic;
using System.Globalization;
using AzureCostCli.CostApi;
using CsvHelper;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public class CsvOutputFormatter : BaseOutputFormatter
{
    public override Task WriteAccumulatedCost(AccumulatedCostSettings settings, IEnumerable<CostItem> costs, IEnumerable<CostItem> forecastedCosts,
        IEnumerable<CostNamedItem> byServiceNameCosts, IEnumerable<CostNamedItem> byLocationCosts, IEnumerable<CostNamedItem> byResourceGroupCosts)
    {
        using (var writer = new StringWriter())
        using (var csv = new CsvWriter(writer, CultureInfo.CurrentCulture))
        {
            csv.WriteRecords(costs);
        
            Console.Write(writer.ToString());
        }
        
        return Task.CompletedTask;
    }

    public override Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources)
    {
     
      
        using (var writer = new StringWriter())
        using (var csv = new CsvWriter(writer, CultureInfo.CurrentCulture))
        {
            csv.WriteRecords(resources);
        
            Console.Write(writer.ToString());
        }
        
        return Task.CompletedTask;
    }
}