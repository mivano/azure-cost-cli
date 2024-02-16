using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using AzureCostCli.Commands;
using Spectre.Console;
using Spectre.Console.Json;

namespace AzureCostCli.CostApi;

public class AzureCostApiRetriever : ICostRetriever
{
    private readonly HttpClient _client;
    private bool _tokenRetrieved;

    public enum DimensionNames
    {
        PublisherType,
        ResourceGroupName,
        ResourceLocation,
        ResourceId,
        ServiceName,
        ServiceTier,
        ServiceFamily,
        InvoiceId,
        CustomerName,
        PartnerName,
        ResourceType,
        ChargeType,
        BillingPeriod,
        MeterCategory,
        MeterSubCategory,
        // Add more dimension names as needed
    }

    public AzureCostApiRetriever(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("CostApi");
    }



    private async Task RetrieveToken(bool includeDebugOutput)
    {
        if (_tokenRetrieved)
            return;

        // Get the token by using the DefaultAzureCredential, but try the AzureCliCredential first
        var tokenCredential = new ChainedTokenCredential(
            new AzureCliCredential(),
            new DefaultAzureCredential());

        if (includeDebugOutput)
            AnsiConsole.WriteLine($"Using token credential: {tokenCredential.GetType().Name} to fetch a token.");

        var token = await tokenCredential.GetTokenAsync(new TokenRequestContext(new[]
            { $"https://management.azure.com/.default" }));

        if (includeDebugOutput)
            AnsiConsole.WriteLine($"Token retrieved and expires at: {token.ExpiresOn}");

        // Set as the bearer token for the HTTP client
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        _tokenRetrieved = true;
    }


    private object? GenerateFilters(string[]? filterArgs)
    {
        if (filterArgs == null || filterArgs.Length == 0)
            return null;

        var filters = new List<object>();
        foreach (var arg in filterArgs)
        {
            var filterParts = arg.Split('=');
            var name = filterParts[0];
            var values = filterParts[1].Split(';');

            // Define default filter dictionary
            var filterDict = new Dictionary<string, object>()
            {
                { "Name", name },
                { "Operator", "In" },
                { "Values", new List<string>(values) }
            };

            // Decide if this is a Dimension or a Tag filter
            if (Enum.IsDefined(typeof(DimensionNames), name))
            {
                filters.Add(new { Dimensions = filterDict });
            }
            else
            {
                filters.Add(new { Tags = filterDict });
            }
        }

        if (filters.Count > 1)
            return new
            {
                And = filters
            };
        else
            return filters[0];
    }

    private Uri DeterminePath(Scope scope, string path)
    {
        // return the scope.ScopePath combined with the path
        return new Uri(scope.ScopePath + path, UriKind.Relative);
        
    }

    private async Task<HttpResponseMessage> ExecuteCallToCostApi(bool includeDebugOutput, object? payload, Uri uri)
    {
        await RetrieveToken(includeDebugOutput);

        if (includeDebugOutput)
        {
            AnsiConsole.WriteLine($"Retrieving data from {uri} using the following payload:");
            AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(payload)));
            AnsiConsole.WriteLine();
        }

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var response = payload == null
            ? await _client.GetAsync(uri)
            : await _client.PostAsJsonAsync(uri, payload, options);

        if (includeDebugOutput)
        {
            AnsiConsole.WriteLine(
                $"Response status code is {response.StatusCode} and got payload size of {response.Content.Headers.ContentLength}");
            if (!response.IsSuccessStatusCode)
            {
                AnsiConsole.WriteLine($"Response content: {await response.Content.ReadAsStringAsync()}");
            }
        }

        response.EnsureSuccessStatusCode();
        return response;
    }

    public async Task<IEnumerable<CostItem>> RetrieveCosts(bool includeDebugOutput, Scope scope,
        string[] filter, MetricType metric,
        TimeframeType timeFrame, DateOnly from, DateOnly to)
    {
        var filters = GenerateFilters(filter);
        var uri = DeterminePath(scope, "/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000");
        
        var payload = new
        {
            type = metric.ToString(),
            timeframe = timeFrame.ToString(),
            timePeriod = timeFrame == TimeframeType.Custom
                ? new
                {
                    from = from.ToString("yyyy-MM-dd"),
                    to = to.ToString("yyyy-MM-dd")
                }
                : null,
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
                    totalCostUSD = new
                    {
                        name = "CostUSD",
                        function = "Sum"
                    }
                },
                filter = filters,
                sorting = new[]
                {
                    new
                    {
                        direction = "Ascending",
                        name = "UsageDate"
                    }
                }
            }
        };

        var response = await ExecuteCallToCostApi(includeDebugOutput, payload, uri);

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

   

    public async Task<IEnumerable<CostNamedItem>> RetrieveCostByServiceName(bool includeDebugOutput,
        Scope scope, string[] filter, MetricType metric, TimeframeType timeFrame, DateOnly from, DateOnly to)
    {        
        var uri = DeterminePath(scope, "/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000");
        
        var payload = new
        {
            type = metric.ToString(),
            timeframe = timeFrame.ToString(),
            timePeriod = timeFrame == TimeframeType.Custom
                ? new
                {
                    from = from.ToString("yyyy-MM-dd"),
                    to = to.ToString("yyyy-MM-dd")
                }
                : null,
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
                    totalCostUSD = new
                    {
                        name = "CostUSD",
                        function = "Sum"
                    }
                },
                sorting = new[]
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
                filter = GenerateFilters(filter)
            }
        };
        var response = await ExecuteCallToCostApi(includeDebugOutput, payload, uri);

        CostQueryResponse? content = await response.Content.ReadFromJsonAsync<CostQueryResponse>();

        var items = new List<CostNamedItem>();
        foreach (var row in content.properties.rows)
        {
            var serviceName = row[2].ToString();
            var value = double.Parse(row[0].ToString(), CultureInfo.InvariantCulture);
            var valueUsd = double.Parse(row[1].ToString(), CultureInfo.InvariantCulture);

            var currency = row[3].ToString();

            var costItem = new CostNamedItem(serviceName, value, valueUsd, currency);
            items.Add(costItem);
        }

        return items;
    }

    public async Task<IEnumerable<CostNamedItem>> RetrieveCostByLocation(bool includeDebugOutput, Scope scope,
        string[] filter,MetricType metric,
        TimeframeType timeFrame, DateOnly from, DateOnly to)
    {
        var uri = DeterminePath(scope, "/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000");

        var payload = new
        {
            type = metric.ToString(),
            timeframe = timeFrame.ToString(),
            timePeriod = timeFrame == TimeframeType.Custom
                ? new
                {
                    from = from.ToString("yyyy-MM-dd"),
                    to = to.ToString("yyyy-MM-dd")
                }
                : null,
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
                    totalCostUSD = new
                    {
                        name = "CostUSD",
                        function = "Sum"
                    }
                },
                sorting = new[]
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
                        name = "ResourceLocation"
                    }
                },
                filter = GenerateFilters(filter)
            }
        };
        var response = await ExecuteCallToCostApi(includeDebugOutput, payload, uri);

        CostQueryResponse? content = await response.Content.ReadFromJsonAsync<CostQueryResponse>();

        var items = new List<CostNamedItem>();
        foreach (var row in content.properties.rows)
        {
            var location = row[2].ToString();
            var value = double.Parse(row[0].ToString(), CultureInfo.InvariantCulture);
            var valueUsd = double.Parse(row[1].ToString(), CultureInfo.InvariantCulture);

            var currency = row[3].ToString();

            var costItem = new CostNamedItem(location, value, valueUsd, currency);
            items.Add(costItem);
        }

        return items;
    }

    public async Task<IEnumerable<CostNamedItem>> RetrieveCostByResourceGroup(bool includeDebugOutput,
        Scope scope, string[] filter,MetricType metric,
        TimeframeType timeFrame, DateOnly from, DateOnly to)
    {
        var uri = DeterminePath(scope, "/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000");

        var payload = new
        {
            type = metric.ToString(),
            timeframe = timeFrame.ToString(),
            timePeriod = timeFrame == TimeframeType.Custom
                ? new
                {
                    from = from.ToString("yyyy-MM-dd"),
                    to = to.ToString("yyyy-MM-dd")
                }
                : null,
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
                    totalCostUSD = new
                    {
                        name = "CostUSD",
                        function = "Sum"
                    }
                },
                sorting = new[]
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
                        name = "ResourceGroupName"
                    },
                    new
                    {
                        type = "Dimension",
                        name = "ChargeType"
                    }
                },
                filter = GenerateFilters(filter)
            }
        };
        var response = await ExecuteCallToCostApi(includeDebugOutput, payload, uri);

        CostQueryResponse? content = await response.Content.ReadFromJsonAsync<CostQueryResponse>();

        var items = new List<CostNamedItem>();
        foreach (var row in content.properties.rows)
        {
            var resourceGroupName = row[2].ToString();
            var value = double.Parse(row[0].ToString(), CultureInfo.InvariantCulture);
            var valueUsd = double.Parse(row[1].ToString(), CultureInfo.InvariantCulture);

            var currency = row[4].ToString();

            var costItem = new CostNamedItem(resourceGroupName, value, valueUsd, currency);
            items.Add(costItem);
        }

        return items;
    }

    public async Task<IEnumerable<CostNamedItem>> RetrieveCostBySubscription(bool includeDebugOutput,
       Scope scope, string[] filter, MetricType metric,
        TimeframeType timeFrame, DateOnly from, DateOnly to)
    {
        var uri = DeterminePath(scope, "/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000");
        
        var payload = new
        {
            type = metric.ToString(),
            timeframe = timeFrame.ToString(),
            timePeriod = timeFrame == TimeframeType.Custom
                ? new
                {
                    from = from.ToString("yyyy-MM-dd"),
                    to = to.ToString("yyyy-MM-dd")
                }
                : null,
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
                    totalCostUSD = new
                    {
                        name = "CostUSD",
                        function = "Sum"
                    }
                },
                sorting = new[]
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
                        name = "SubscriptionName"
                    },
                    new
                    {
                        type = "Dimension",
                        name = "ChargeType"
                    }
                },
                filter = GenerateFilters(filter)
            }
        };
        var response = await ExecuteCallToCostApi(includeDebugOutput, payload, uri);

        CostQueryResponse? content = await response.Content.ReadFromJsonAsync<CostQueryResponse>();

        var items = new List<CostNamedItem>();
        foreach (var row in content.properties.rows)
        {
            var subscriptionName = row[2].ToString();
            var value = double.Parse(row[0].ToString(), CultureInfo.InvariantCulture);
            var valueUsd = double.Parse(row[1].ToString(), CultureInfo.InvariantCulture);

            var currency = row[4].ToString();

            var costItem = new CostNamedItem(subscriptionName, value, valueUsd, currency);
            items.Add(costItem);
        }

        return items;
    }

    public async Task<IEnumerable<CostDailyItem>> RetrieveDailyCost(bool includeDebugOutput,
        Scope scope, string[] filter, MetricType metric, string dimension,
        TimeframeType timeFrame, DateOnly from, DateOnly to, bool includeTags)
    {
        var uri = DeterminePath(scope, "/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000");

        var payload = new
        {
            type = metric.ToString(),
            timeframe = timeFrame.ToString(),
            timePeriod = timeFrame == TimeframeType.Custom
                ? new
                {
                    from = from.ToString("yyyy-MM-dd"),
                    to = to.ToString("yyyy-MM-dd")
                }
                : null,
            dataSet = new
            {
                granularity = "Daily",
                include = includeTags ? new[] { "Tags" } : null,
                aggregation = new
                {
                    totalCost = new
                    {
                        name = "Cost",
                        function = "Sum"
                    },
                    totalCostUSD = new
                    {
                        name = "CostUSD",
                        function = "Sum"
                    }
                },
                sorting = new[]
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
                        name = dimension
                    },
                    new
                    {
                        type = "Dimension",
                        name = "ChargeType"
                    }
                },
                filter = GenerateFilters(filter)
            }
        };
        var response = await ExecuteCallToCostApi(includeDebugOutput, payload, uri);

        CostQueryResponse? content = await response.Content.ReadFromJsonAsync<CostQueryResponse>();

        var items = new List<CostDailyItem>();
        foreach (var row in content.properties.rows)
        {
            var resourceGroupName = row[3].ToString();
            var date = DateOnly.ParseExact(row[2].ToString(), "yyyyMMdd", CultureInfo.InvariantCulture);

            var value = double.Parse(row[0].ToString(), CultureInfo.InvariantCulture);
            var valueUsd = double.Parse(row[1].ToString(), CultureInfo.InvariantCulture);

            // if includeTags is true, row[5] is the tag, and row[6] is the currency, otherwise row[5] is the currency
            var currency = row[5].ToString();
            Dictionary<string, string>? tags =null;

            // if includeTags is true, switch the value between currency and tags
            // that's the order how the API REST exposes the resultset
            if (includeTags)
            {
                var tagsArray = row[5].EnumerateArray().ToArray();
                
                tags = new Dictionary<string, string>();
                
                foreach (var tagString in tagsArray)
                {
                    var parts = tagString.GetString().Split(':');
                    if (parts.Length == 2) // Ensure the string is in the format "key:value"
                    {
                        var key = parts[0].Trim('"'); // Remove quotes from the key
                        var tagValue = parts[1].Trim('"'); // Remove quotes from the value
                        tags[key] = tagValue;
                    }
                }
                currency = row[6].ToString();
            }

            var costItem = new CostDailyItem(date, resourceGroupName, value, valueUsd, currency, tags);
            items.Add(costItem);
        }

        return items;
    }

    public async Task<Subscription> RetrieveSubscription(bool includeDebugOutput, Guid subscriptionId)
    {
        var uri = new Uri(
            $"/subscriptions/{subscriptionId}/?api-version=2019-11-01",
            UriKind.Relative);

        var response = await ExecuteCallToCostApi(includeDebugOutput, null, uri);

        var content = await response.Content.ReadFromJsonAsync<Subscription>();

        if (includeDebugOutput)
        {
            var json = JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true });
            AnsiConsole.WriteLine("Retrieved subscription details:");
            AnsiConsole.Write(new JsonText(json));
            AnsiConsole.WriteLine();
        }

        return content;
    }

    public async Task<IEnumerable<CostItem>> RetrieveForecastedCosts(bool includeDebugOutput, Scope scope,
        string[] filter, MetricType metric,
        TimeframeType timeFrame, DateOnly from, DateOnly to)
    {      
        var uri = DeterminePath(scope, "/providers/Microsoft.CostManagement/forecast?api-version=2021-10-01&$top=5000");

        var payload = new
        {
            type = metric.ToString(),
            timeframe = timeFrame.ToString(),
            timePeriod = timeFrame == TimeframeType.Custom
                ? new
                {
                    from = from.ToString("yyyy-MM-dd"),
                    to = to.ToString("yyyy-MM-dd")
                }
                : null,
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
                filter = GenerateFilters(filter),
                sorting = new[]
                {
                    new
                    {
                        direction = "ascending",
                        name = "UsageDate"
                    }
                }
            }
        };

        var items = new List<CostItem>();

        try
        {
            // Allow this one to fail, as it is not supported for all subscriptions
            var response = await ExecuteCallToCostApi(includeDebugOutput, payload, uri);

            CostQueryResponse? content = await response.Content.ReadFromJsonAsync<CostQueryResponse>();


            foreach (var row in content.properties.rows)
            {
                var date = DateOnly.ParseExact(row[1].ToString(), "yyyyMMdd", CultureInfo.InvariantCulture);
                var value = double.Parse(row[0].ToString(), CultureInfo.InvariantCulture);

                var currency = row[3].ToString();

                var costItem = new CostItem(date, value, value, currency);
                items.Add(costItem);
            }
        }
        catch (Exception ex)
        {
            // Eat exception, we log anyway with the debug output
            if (includeDebugOutput)
            {
                AnsiConsole.WriteException(ex);
                AnsiConsole.WriteLine("Ignoring this exception, as it is not supported for all subscriptions.");
            }
        }

        return items;
    }

    public async Task<IEnumerable<CostResourceItem>> RetrieveCostForResources(bool includeDebugOutput,
        Scope scope, string[] filter, MetricType metric, bool excludeMeterDetails, TimeframeType timeFrame,
        DateOnly from,
        DateOnly to)
    {
        var uri = DeterminePath(scope, "/providers/Microsoft.CostManagement/query?api-version=2023-03-01&$top=5000");

        object grouping;
        if (excludeMeterDetails == false)
            grouping = new[]
            {
                new
                {
                    type = "Dimension",
                    name = "ResourceId"
                },
                new
                {
                    type = "Dimension",
                    name = "ResourceType"
                },
                new
                {
                    type = "Dimension",
                    name = "ResourceLocation"
                },
                new
                {
                    type = "Dimension",
                    name = "ChargeType"
                },
                new
                {
                    type = "Dimension",
                    name = "ResourceGroupName"
                },
                new
                {
                    type = "Dimension",
                    name = "PublisherType"
                },
                new
                {
                    type = "Dimension",
                    name = "MeterCategory"
                },
                new
                {
                    type = "Dimension",
                    name = "MeterSubcategory"
                },
                new
                {
                    type = "Dimension",
                    name = "Meter"
                }
            };
        else
        {
            grouping = new[]
            {
                new
                {
                    type = "Dimension",
                    name = "ResourceId"
                },
                new
                {
                    type = "Dimension",
                    name = "ResourceType"
                },
                new
                {
                    type = "Dimension",
                    name = "ResourceLocation"
                },
                new
                {
                    type = "Dimension",
                    name = "ChargeType"
                },
                new
                {
                    type = "Dimension",
                    name = "ResourceGroupName"
                },
                new
                {
                    type = "Dimension",
                    name = "PublisherType"
                }
            };
        }

        var payload = new
        {
            type = metric.ToString(),
            timeframe = timeFrame.ToString(),
            timePeriod = timeFrame == TimeframeType.Custom
                ? new
                {
                    from = from.ToString("yyyy-MM-dd"),
                    to = to.ToString("yyyy-MM-dd")
                }
                : null,
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
                    totalCostUSD = new
                    {
                        name = "CostUSD",
                        function = "Sum"
                    }
                },
                include = new[] { "Tags" },
                filter = GenerateFilters(filter),
                grouping = grouping,
            }
        };
        var response = await ExecuteCallToCostApi(includeDebugOutput, payload, uri);

        CostQueryResponse? content = await response.Content.ReadFromJsonAsync<CostQueryResponse>();

        var items = new List<CostResourceItem>();
        foreach (JsonElement row in content.properties.rows)
        {
            double cost = row[0].GetDouble();
            double costUSD = row[1].GetDouble();
            string resourceId = row[2].GetString();
            string resourceType = row[3].GetString();
            string resourceLocation = row[4].GetString();
            string chargeType = row[5].GetString();
            string resourceGroupName = row[6].GetString();
            string publisherType = row[7].GetString();

            string serviceName = excludeMeterDetails ? null : row[8].GetString();
            string serviceTier = excludeMeterDetails ? null : row[9].GetString();
            string meter = excludeMeterDetails ? null : row[10].GetString();

            int tagsColumn = excludeMeterDetails ? 8 : 11;
            // Assuming row[tagsColumn] contains the tags array
            var tagsArray = row[tagsColumn].EnumerateArray().ToArray();

            Dictionary<string, string> tags = new Dictionary<string, string>();

            foreach (var tagString in tagsArray)
            {
                var parts = tagString.GetString().Split(':');
                if (parts.Length == 2) // Ensure the string is in the format "key:value"
                {
                    var key = parts[0].Trim('"'); // Remove quotes from the key
                    var value = parts[1].Trim('"'); // Remove quotes from the value
                    tags[key] = value;
                }
            }

            int currencyColumn = excludeMeterDetails ? 9 : 12;
            string currency = row[currencyColumn].GetString();

            CostResourceItem item = new CostResourceItem(cost, costUSD, resourceId, resourceType, resourceLocation,
                chargeType, resourceGroupName, publisherType, serviceName, serviceTier, meter, tags, currency);

            items.Add(item);
        }

        if (excludeMeterDetails)
        {
            // As we do not care about the meter details, we still have the possibility of resources with the same, but having multiple locations like Intercontinental, Unknown and Unassigned
            // We need to aggregate these resources together and show the total cost for the resource, the resource locations need to be combined as well. So it can become West Europe, Intercontinental

            var aggregatedItems = new List<CostResourceItem>();
            var groupedItems = items.GroupBy(x => x.ResourceId);
            foreach (var groupedItem in groupedItems)
            {
                var aggregatedItem = new CostResourceItem(groupedItem.Sum(x => x.Cost), groupedItem.Sum(x => x.CostUSD),
                    groupedItem.Key, groupedItem.First().ResourceType,
                    string.Join(", ", groupedItem.Select(x => x.ResourceLocation)), groupedItem.First().ChargeType,
                    groupedItem.First().ResourceGroupName, groupedItem.First().PublisherType, null, null, null,
                    groupedItem.First().Tags, groupedItem.First().Currency);
                aggregatedItems.Add(aggregatedItem);
            }

            return aggregatedItems;
        }

        return items;
    }

    public async Task<IEnumerable<UsageDetails>> RetrieveUsageDetails(bool includeDebugOutput,
        Scope scope, string filter,  DateOnly from, DateOnly to)
    {
        var uri = DeterminePath(scope, "/providers/Microsoft.Consumption/usageDetails?api-version=2023-05-01&$expand=meterDetails&metric=usage&$top=5000");

        filter = (!string.IsNullOrWhiteSpace(filter)
            ?   filter + " AND "
            : "") +"properties/usageStart ge '" + from.ToString("yyyy-MM-dd") + "' and properties/usageEnd le '" +
                 to.ToString("yyyy-MM-dd") + "'";


        uri = new Uri($"{uri}&$filter={filter}", UriKind.Relative);

        var items = new List<UsageDetails>();

        while (uri != null)
        {
            var response = await ExecuteCallToCostApi(includeDebugOutput, null, uri);

            UsageDetailsResponse payload = await response.Content.ReadFromJsonAsync<UsageDetailsResponse>() ??
                                           new UsageDetailsResponse();

            items.AddRange(payload.value);
            uri = payload.nextLink != null ? new Uri(payload.nextLink, UriKind.Relative) : null;
        }

        return items;
    }

    public async Task<IEnumerable<BudgetItem>> RetrieveBudgets(bool includeDebugOutput, Scope scope)
    {
        var uri = DeterminePath(scope, "/providers/Microsoft.Consumption/budgets/?api-version=2021-10-01");

        var response = await ExecuteCallToCostApi(includeDebugOutput, null, uri);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var items = root.GetProperty("value");

        List<BudgetItem> budgetItems = new List<BudgetItem>();

        foreach (var item in items.EnumerateArray())
        {
            var properties = item.GetProperty("properties");

            var id = item.GetProperty("id").GetString();
            var name = item.GetProperty("name").GetString();
            var amount = properties.GetProperty("amount").GetDouble();
            var timeGrain = properties.GetProperty("timeGrain").GetString();

            var timePeriod = properties.GetProperty("timePeriod");
            var startDate = DateTime.Parse(timePeriod.GetProperty("startDate").GetString());
            var endDate = DateTime.Parse(timePeriod.GetProperty("endDate").GetString());

            double? currentSpendAmount = null;
            string currentSpendCurrency = null;
            if (properties.TryGetProperty("currentSpend", out var currentSpend))
            {
                currentSpendAmount = currentSpend.GetProperty("amount").GetDouble();
                currentSpendCurrency = currentSpend.GetProperty("unit").GetString();
            }

            double? forecastAmount = null;
            string forecastCurrency = null;
            if (properties.TryGetProperty("forecastSpend", out var forecastSpend))
            {
                forecastAmount = forecastSpend.GetProperty("amount").GetDouble();
                forecastCurrency = forecastSpend.GetProperty("unit").GetString();
            }

            List<Notification> notifications = null;
            if (properties.TryGetProperty("notifications", out var notificationsElement))
            {
                notifications = new List<Notification>();
                foreach (var notificationProperty in notificationsElement.EnumerateObject())
                {
                    var enabled = notificationProperty.Value.GetProperty("enabled").GetBoolean();
                    var operatorValue = notificationProperty.Value.GetProperty("operator").GetString();
                    var threshold = notificationProperty.Value.GetProperty("threshold").GetDouble();

                    var contactEmails = notificationProperty.Value.GetProperty("contactEmails").EnumerateArray()
                        .Select(x => x.GetString()).ToList();
                    var contactRoles = notificationProperty.Value.GetProperty("contactRoles").EnumerateArray()
                        .Select(x => x.GetString()).ToList();

                    List<string> contactGroups = null;
                    if (notificationProperty.Value.TryGetProperty("contactGroups", out var contactGroupsElement))
                    {
                        contactGroups = contactGroupsElement.EnumerateArray().Select(x => x.GetString()).ToList();
                    }

                    var notification = new Notification(notificationProperty.Name, enabled, operatorValue, threshold,
                        contactEmails, contactRoles, contactGroups);

                    notifications.Add(notification);
                }
            }

            var budgetItem = new BudgetItem(name, id, amount, timeGrain, startDate, endDate, currentSpendAmount,
                currentSpendCurrency, forecastAmount, forecastCurrency, notifications);
            budgetItems.Add(budgetItem);
        }

        return budgetItems;
    }
}

public class UsageDetailsResponse
{
    public UsageDetails[] value { get; set; }
    public string? nextLink { get; set; }
}

public class UsageDetails
{
    public string kind { get; set; }
    public string id { get; set; }
    public string name { get; set; }
    public string type { get; set; }
    public Dictionary<string, string> tags { get; set; }
    public UsageProperties properties { get; set; }
}

public class UsageProperties
{
    public string billingPeriodStartDate { get; set; }
    public string billingPeriodEndDate { get; set; }
    public string billingProfileId { get; set; }
    public string billingProfileName { get; set; }
    public string subscriptionId { get; set; }
    public string subscriptionName { get; set; }
    public string date { get; set; }
    public string product { get; set; }
    public string meterId { get; set; }
    public double quantity { get; set; }
    public double effectivePrice { get; set; }
    public double cost { get; set; }
    public double unitPrice { get; set; }
    public string billingCurrency { get; set; }
    public string resourceLocation { get; set; }
    public string consumedService { get; set; }
    public string resourceId { get; set; }
    public string resourceName { get; set; }
    public string additionalInfo { get; set; }
    public string resourceGroup { get; set; }
    public string offerId { get; set; }
    public bool isAzureCreditEligible { get; set; }
    public string publisherType { get; set; }
    public string chargeType { get; set; }
    public string frequency { get; set; }
    public MeterDetails meterDetails { get; set; }
}

public class MeterDetails
{
    public string meterName { get; set; }
    public string meterCategory { get; set; }
    public string meterSubCategory { get; set; }
    public string unitOfMeasure { get; set; }
}