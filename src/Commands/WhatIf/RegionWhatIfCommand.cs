using System.Collections.Concurrent;
using AzureCostCli.Commands.ShowCommand.OutputFormatters;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.WhatIf;

// Run what-if scenarios to check price difference if the resources would have run in a different region
public class RegionWhatIfCommand : AsyncCommand<WhatIfSettings>
{
    private readonly IPriceRetriever _priceRetriever;
    private readonly ICostRetriever _costRetriever;

    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = new();

    private ConcurrentDictionary<string, CacheEntry> _cache = new();
    private ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private TimeSpan _cacheLifetime = TimeSpan.FromHours(1); // Cache lifetime can be adjusted as needed

    public RegionWhatIfCommand(IPriceRetriever priceRetriever, ICostRetriever costRetriever)
    {
        _priceRetriever = priceRetriever;
        _costRetriever = costRetriever;

        // Add the output formatters
        _outputFormatters.Add(OutputFormat.Console, new ConsoleOutputFormatter());
        _outputFormatters.Add(OutputFormat.Json, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Jsonc, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Text, new TextOutputFormatter());
        _outputFormatters.Add(OutputFormat.Markdown, new MarkdownOutputFormatter());
        _outputFormatters.Add(OutputFormat.Csv, new CsvOutputFormatter());
    }

    public override async Task<int> ExecuteAsync(CommandContext context, WhatIfSettings settings)
    {
        // Get the subscription ID from the settings
        var subscriptionId = settings.Subscription;

        if (subscriptionId == Guid.Empty)
        {
            // Get the subscription ID from the Azure CLI
            try
            {
                if (settings.Debug)
                    AnsiConsole.WriteLine(
                        "No subscription ID specified. Trying to retrieve the default subscription ID from Azure CLI.");

                subscriptionId = Guid.Parse(AzCommand.GetDefaultAzureSubscriptionId());

                if (settings.Debug)
                    AnsiConsole.WriteLine($"Default subscription ID retrieved from az cli: {subscriptionId}");

                settings.Subscription = subscriptionId;
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(new ArgumentException(
                    "Missing subscription ID. Please specify a subscription ID or login to Azure CLI.", e));
                return -1;
            }
        }

        // Fetch the costs from the Azure Cost Management API
        IEnumerable<CostResourceItem> resources = new List<CostResourceItem>();



        await AnsiConsole.Status()
            .StartAsync("Fetching cost data for resources...", async ctx =>
            {
                resources = await _costRetriever.RetrieveCostForResources(
                    settings.Debug,
                    subscriptionId, settings.Filter,
                    settings.Metric,
                    false,
                    settings.Timeframe,
                    settings.From,
                    settings.To);

                ctx.Status = "Running What-If analysis...";

                List<Task> tasks = new List<Task>();
              
                foreach (var resource in resources)
                {
                    if (resource.ResourceType == "microsoft.compute/virtualmachines" && resource.ServiceName == "Virtual Machines")
                    {
                        string skuName = resource.Meter;

                       var items = await FetchPricesForAllRegions(skuName);
                        
                        foreach (var item in items.OrderBy(a=>a.Price))
                        {
                            AnsiConsole.MarkupLine($"[bold]{resource.GetResourceName()}[/] in [bold]{item.Region}[/] would cost [bold]{item.Price}[/] per hour");
                        }

                       
                    }
                }

                // Wait for all tasks to complete
               // await Task.WhenAll(tasks);
                
            });


        return 0;
    }

    private async Task<IEnumerable<RegionPrice>> FetchPricesForAllRegions(string skuName)
    {
        string filter = $"serviceName eq 'Virtual Machines' and skuName eq '{skuName}' and type eq 'Consumption'";
        IEnumerable<PriceRecord> prices = await _priceRetriever.GetAzurePricesAsync(filter);

        var items = new List<RegionPrice>();
        foreach (var price in prices)
        {
           items.Add(new RegionPrice(price.ArmRegionName, price.RetailPrice));
        }

        return items;
    }

}

public record RegionPrice(string Region, double Price);


