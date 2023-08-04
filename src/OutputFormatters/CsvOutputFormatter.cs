using System.Dynamic;
using System.Globalization;
using AzureCostCli.Commands.Regions;
using AzureCostCli.CostApi;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

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

    public override Task WriteAnomalyDetectionResults(DetectAnomalySettings settings, List<AnomalyDetectionResult> anomalies)
    {
        return ExportToCsv(settings.SkipHeader, anomalies);
    }

    public override Task WriteRegions(RegionsSettings settings, IReadOnlyCollection<AzureRegion> regions)
    {
        return ExportToCsv(settings.SkipHeader, regions);
    }

    public override Task WriteCostByTag(CostByTagSettings settings, Dictionary<string, Dictionary<string, List<CostResourceItem>>> byTags)
    {
        // Flatten the hierarchy to a single list, including the tag and value
        var resourcesWithTagAndValue = new List<dynamic>();
        foreach (var (tag, value) in byTags)
        {
            foreach (var (tagValue, resources) in value)
            {
                foreach (var resource in resources)
                {
                    dynamic expando = new ExpandoObject();
                    expando.Tag = tag;
                    expando.Value = tagValue;
                    expando.ResourceId = resource.ResourceId;
                    expando.ResourceType = resource.ResourceType;
                    expando.ResourceGroup = resource.ResourceGroupName;
                    expando.ResourceLocation = resource.ResourceLocation;
                    expando.Cost = resource.Cost;
                    expando.Currency = resource.Currency;
                    expando.CostUsd = resource.CostUSD;
                    
                    resourcesWithTagAndValue.Add(expando);
                }
            }
        }
      
        
        return ExportToCsv(settings.SkipHeader, resourcesWithTagAndValue);
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
            csv.Context.TypeConverterCache.AddConverter<double>(new CustomDoubleConverter());
            csv.WriteRecords(resources);

            Console.Write(writer.ToString());
        }

        return Task.CompletedTask;
    }

   
    
    
}

public class CustomDoubleConverter : DoubleConverter
{
    public override string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
    {
        double number = (double)value;
        return number.ToString("F8", CultureInfo.InvariantCulture);
    }
}