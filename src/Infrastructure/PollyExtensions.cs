using System.Net;
using Polly;

namespace AzureCostCli.Infrastructure;

// ReSharper disable once ClassNeverInstantiated.Global
public class PollyExtensions
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryAfterPolicy()
    {
        return Policy.HandleResult<HttpResponseMessage>
                (msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: (_, response, _) =>
                {
                    var retryAfterHeader = 
                        response.Result.Headers.FirstOrDefault(h => h.Key.ToLowerInvariant().Contains("retry-after"));
                    return retryAfterHeader.Key != null && int.TryParse(retryAfterHeader.Value.First(), out var seconds)
                        ? TimeSpan.FromSeconds(seconds)
                        : TimeSpan.FromSeconds(5);
                },
                onRetryAsync: (msg, time, retries, context) => Task.CompletedTask
            );
    }
}