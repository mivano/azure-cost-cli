using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.Core;
using Azure.Identity;
using Spectre.Console;
using Spectre.Console.Cli;

public class ShowCommand : AsyncCommand<ShowSettings>
{
    private readonly HttpClient _client;
    private readonly Dictionary<OutputFormat, OutputFormatter> _outputFormatters = new ();

    public override ValidationResult Validate(CommandContext context, ShowSettings settings)
    {
   
        return settings.Subscription == Guid.Empty 
            ? ValidationResult.Error("A subscription ID must be specified.")
            : ValidationResult.Success();
    
    }

    public ShowCommand()
    {
        _client = new HttpClient();
        _client.BaseAddress = new Uri("https://management.azure.com/");
        
        _outputFormatters.Add(OutputFormat.Console, new ConsoleOutputFormatter());
        _outputFormatters.Add(OutputFormat.Json, new JsonOutputFormatter());
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context, ShowSettings settings)
    {
        // Get the token by using the DefaultAzureCredential
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions {});
        var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { $"https://management.azure.com/.default" }));
        
        // Set as the bearer token for the HTTP client
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        // Get the subscription ID from the settings
        var subscriptionId = settings.Subscription;
        
        // Fetch the costs
        var costs = await RetrieveCosts(subscriptionId,  settings.Timeframe, settings.From, settings.To);
        var forecastedCosts = await RetrieveForecastedCosts(subscriptionId);
        var byServiceNameCosts = await RetrieveCostByServiceName(subscriptionId, settings.Timeframe, settings.From, settings.To);
        
        // Write the output
        await _outputFormatters[settings.Output].WriteOutput(settings, costs, forecastedCosts, byServiceNameCosts);
        
        return 0;
    }


    private async Task<IEnumerable<CostItem>> RetrieveCosts(Guid subscriptionId, TimeframeType timeFrame, DateOnly from, DateOnly to)
    {
        var uri = new Uri(
            $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2021-10-01&$top=5000", UriKind.Relative);
        
        var payload = new
        {
            type = "ActualCost",
            timeframe = timeFrame.ToString(),
            timePeriod = timeFrame == TimeframeType.Custom ? new
            {
                from = from.ToString("yyyy-MM-dd"),
                to = to.ToString("yyyy-MM-dd")
            } : null,
            dataSet = new
            {
                granularity = "Daily",
                aggregation = new
                {
                    totalCost = new
                    {
                        name = "Cost",
                        function = "Sum"
                    },
                    totalCostUSD = new  {
                        name = "CostUSD",
                        function =  "Sum"
                    }
                },
                sorting= new[]
                {
                    new
                    {
                        direction = "Ascending",
                        name = "UsageDate"
                    }
                }
              
            }
        };
        var response = await _client.PostAsJsonAsync(uri, payload);
        response.EnsureSuccessStatusCode();
        
        CostQueryResponse? content = await response.Content.ReadFromJsonAsync<CostQueryResponse>();

        var items = new List<CostItem>();
        foreach (var row in content.properties.rows)
        {
            var date = DateOnly.ParseExact(row[2].ToString(), "yyyyMMdd", CultureInfo.InvariantCulture);
            var value = double.Parse(row[0].ToString(), CultureInfo.InvariantCulture);
            var valueUsd = double.Parse(row[1].ToString(), CultureInfo.InvariantCulture);

            var currency = row[3].ToString();
          
            var costItem = new CostItem(date, value, valueUsd, currency);
            items.Add(costItem);
        }

        return items;
    }
    
    private async Task<IEnumerable<CostServiceItem>> RetrieveCostByServiceName(Guid subscriptionId, TimeframeType timeFrame, DateOnly from, DateOnly to)
    {
        var uri = new Uri(
            $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2021-10-01&$top=5000", UriKind.Relative);
        
        var payload = new
        {
            type = "ActualCost",
            timeframe = timeFrame.ToString(),
            timePeriod = timeFrame == TimeframeType.Custom ? new
            {
                from = from.ToString("yyyy-MM-dd"),
                to = to.ToString("yyyy-MM-dd")
            } : null,
            dataSet = new
            {
                granularity = "None",
                aggregation = new
                {
                    totalCost = new
                    {
                        name = "Cost",
                        function = "Sum"
                    },
                    totalCostUSD = new  {
                        name = "CostUSD",
                        function =  "Sum"
                    }
                },
                sorting= new[]
                {
                    new
                    {
                        direction = "Ascending",
                        name = "UsageDate"
                    }
                },
                grouping = new[]
                {
                    new
                    {
                        type = "Dimension",
                        name = "ServiceName"
                    }
                },
                filter = new
                {
                    Dimensions = new
                    {
                        Name = "PublisherType",
                        Operator = "In",
                        Values = new [] {"azure"}
                    }
                }
            }
        };
        var response = await _client.PostAsJsonAsync(uri, payload);
        response.EnsureSuccessStatusCode();
        
        CostQueryResponse? content = await response.Content.ReadFromJsonAsync<CostQueryResponse>();

        var items = new List<CostServiceItem>();
        foreach (var row in content.properties.rows)
        {
            var serviceName = row[2].ToString();
            var value = double.Parse(row[0].ToString(), CultureInfo.InvariantCulture);
            var valueUsd = double.Parse(row[1].ToString(), CultureInfo.InvariantCulture);

            var currency = row[3].ToString();
          
            var costItem = new CostServiceItem(serviceName, value, valueUsd, currency);
            items.Add(costItem);
        }

        return items;
    }
   
    private async Task<IEnumerable<CostItem>> RetrieveForecastedCosts(Guid subscriptionId)
    {
        var uri = new Uri(
            $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/forecast?api-version=2021-10-01&$top=5000", UriKind.Relative);
        
        var payload = new
        {
            type = "ActualCost",
            
            dataSet = new
            {
                granularity = "Daily",
                aggregation = new
                {
                    totalCost = new
                    {
                        name = "Cost",
                        function = "Sum"
                    }
                },
                sorting= new[]
                {
                    new
                    {
                        direction = "Ascending",
                        name = "UsageDate"
                    }
                },
                filter = new
                {
                    Dimensions = new
                    {
                        Name = "PublisherType",
                        Operator = "In",
                        Values = new [] {"azure"}
                    }
                }
            }
        };
        var response = await _client.PostAsJsonAsync(uri, payload);
        response.EnsureSuccessStatusCode();
        
        CostQueryResponse? content = await response.Content.ReadFromJsonAsync<CostQueryResponse>();

        var items = new List<CostItem>();
        foreach (var row in content.properties.rows)
        {
            var date = DateOnly.ParseExact(row[1].ToString(), "yyyyMMdd", CultureInfo.InvariantCulture);
            var value = double.Parse(row[0].ToString(), CultureInfo.InvariantCulture);
        
            var currency = row[3].ToString();
          
            var costItem = new CostItem(date, value, value, currency);
            items.Add(costItem);
        }

        return items;
    }
}