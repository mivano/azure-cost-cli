using System.Net;
using Polly;

namespace AzureCostCli.CostApi;

public static class PollyPolicyExtensions
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryAfterPolicy()
    {
        // Define WaitAndRetry policy
        var waitAndRetryPolicy = Policy.HandleResult<HttpResponseMessage>(msg => 
                msg.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: (_, response, _) => {
                    var headers = response.Result?.Headers;
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            if (header.Key.ToLower().Contains("retry-after") && header.Value != null)
                            {
                                if (int.TryParse(header.Value.First(), out int seconds))
                                {
                                    return TimeSpan.FromSeconds(seconds);
                                }
                            }
                        }
                    }
                    // If no header with a retry-after value is found, fall back to 2 seconds.
                    return TimeSpan.FromSeconds(2);
                },
                onRetryAsync: (msg, time, retries, context) => Task.CompletedTask
            );

        // Define Timeout policy        
        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(60));

        // Wrap WaitAndRetry with Timeout
        var resilientPolicy = Policy.WrapAsync(timeoutPolicy, waitAndRetryPolicy);

        return resilientPolicy;

    }
}