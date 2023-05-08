using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using AzureCostCli.Commands;
using Polly;
using Spectre.Console;
using Spectre.Console.Json;

namespace AzureCostCli.CostApi;

public class AzureCostApiRetriever : ICostRetriever
{
    private readonly HttpClient _client;
    private bool _tokenRetrieved;

    public AzureCostApiRetriever(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("CostApi");
    }

    public static IAsyncPolicy<HttpResponseMessage> GetRetryAfterPolicy()
    {
        return Policy.HandleResult<HttpResponseMessage>
            (msg => msg.Headers.TryGetValues("x-ms-ratelimit-microsoft.costmanagement-entity-retry-after",
                out var _))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: (_, response, _) =>
                    response.Result.Headers.TryGetValues("x-ms-ratelimit-microsoft.costmanagement-entity-retry-after",
                        out var seconds)
                        ? TimeSpan.FromSeconds(int.Parse(seconds.First()))
                        : TimeSpan.FromSeconds(5),
                onRetryAsync: (msg, time, retries, context) => Task.CompletedTask
            );
    }

    private async Task RetrieveToken(bool includeDebugOutput)
    {
        if (_tokenRetrieved)
            return;

        // Get the token by using the DefaultAzureCredential
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

    private async Task<HttpResponseMessage> ExecuteCallToCostApi(bool includeDebugOutput, object payload, Uri uri)
    {
        await RetrieveToken(includeDebugOutput);

        if (includeDebugOutput)
        {
            AnsiConsole.WriteLine($"Retrieving data from {uri} using the following payload:");
            AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(payload)));
            AnsiConsole.WriteLine();
        }

        var response = await _client.PostAsJsonAsync(uri, payload);

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

    public async Task<IEnumerable<CostItem>> RetrieveCosts(bool includeDebugOutput, Guid subscriptionId,
        TimeframeType timeFrame, DateOnly from, DateOnly to)
    {
        var uri = new Uri(
            $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2021-10-01&$top=5000",
            UriKind.Relative);

        var payload = new
        {
            type = "ActualCost",
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
        Guid subscriptionId, TimeframeType timeFrame, DateOnly from, DateOnly to)
    {
        var uri = new Uri(
            $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2021-10-01&$top=5000",
            UriKind.Relative);

        var payload = new
        {
            type = "ActualCost",
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
                filter = new
                {
                    Dimensions = new
                    {
                        Name = "PublisherType",
                        Operator = "In",
                        Values = new[] { "azure" }
                    }
                }
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

    public async Task<IEnumerable<CostNamedItem>> RetrieveCostByLocation(bool includeDebugOutput, Guid subscriptionId,
        TimeframeType timeFrame, DateOnly from, DateOnly to)
    {
        var uri = new Uri(
            $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2021-10-01&$top=5000",
            UriKind.Relative);

        var payload = new
        {
            type = "ActualCost",
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
                filter = new
                {
                    Dimensions = new
                    {
                        Name = "PublisherType",
                        Operator = "In",
                        Values = new[] { "azure" }
                    }
                }
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
        Guid subscriptionId,
        TimeframeType timeFrame, DateOnly from, DateOnly to)
    {
        var uri = new Uri(
            $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2021-10-01&$top=5000",
            UriKind.Relative);

        var payload = new
        {
            type = "ActualCost",
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
                filter = new
                {
                    Dimensions = new
                    {
                        Name = "PublisherType",
                        Operator = "In",
                        Values = new[] { "azure" }
                    }
                }
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

            var currency = row[3].ToString();

            var costItem = new CostNamedItem(resourceGroupName, value, valueUsd, currency);
            items.Add(costItem);
        }

        return items;
    }

    public async Task<IEnumerable<CostItem>> RetrieveForecastedCosts(bool includeDebugOutput, Guid subscriptionId,
        TimeframeType timeFrame, DateOnly from, DateOnly to)
    {
        var uri = new Uri(
            $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/forecast?api-version=2021-10-01&$top=5000",
            UriKind.Relative);

        var payload = new
        {
            type = "ActualCost",
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
        Guid subscriptionId, TimeframeType timeFrame, DateOnly from,
        DateOnly to)
    {
        var uri = new Uri(
            $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2021-10-01&$top=5000",
            UriKind.Relative);

        var payload = new
        {
            type = "ActualCost",
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
                        name = "ServiceName"
                    },
                    new
                    {
                        type = "Dimension",
                        name = "ServiceTier"
                    },
                    new
                    {
                        type = "Dimension",
                        name = "Meter"
                    }
                },
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
            string serviceName = row[8].GetString();
            string serviceTier = row[9].GetString();
            string meter = row[10].GetString();
            string[] tags = row[11].EnumerateArray().Select(tag => tag.GetString()).ToArray();
            string currency = row[12].GetString();

            CostResourceItem item = new CostResourceItem(cost, costUSD, resourceId, resourceType, resourceLocation,
                chargeType, resourceGroupName, publisherType, serviceName, serviceTier, meter, tags, currency);

            items.Add(item);
        }

        return items;
    }
}