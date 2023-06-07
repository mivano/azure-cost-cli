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
        var config = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            HasHeaderRecord = settings.SkipHeader == false
        };
        
        using (var writer = new StringWriter())
        using (var csv = new CsvWriter(writer, config))
        {
            csv.WriteRecords(accumulatedCostDetails.Costs);
        
            Console.Write(writer.ToString());
        }
        
        return Task.CompletedTask;
    }

    public override Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources)
    {
        var config = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            HasHeaderRecord = settings.SkipHeader == false
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