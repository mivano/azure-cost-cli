using System.Net;
using Polly;

namespace AzureCostCli.Infrastructure;

public class PollyExtensions
{
    private static string RetryAfterHeader = "x-ms-ratelimit-microsoft.costmanagement-clienttype-retry-after";
    private static string RetryAfterHeader2 = "x-ms-ratelimit-microsoft.costmanagement-entity-retry-after";

    public static IAsyncPolicy<HttpResponseMessage> GetRetryAfterPolicy()
    {
        return Policy.HandleResult<HttpResponseMessage>
                (msg => msg.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: (_, response, _) =>
                    response.Result.Headers.TryGetValues(RetryAfterHeader,
                        out var seconds)
                        ? TimeSpan.FromSeconds(int.Parse(seconds.First()))
                        :  response.Result.Headers.TryGetValues(RetryAfterHeader2,
                            out var seconds2)
                            ? TimeSpan.FromSeconds(int.Parse(seconds2.First())): TimeSpan.FromSeconds(5),
                onRetryAsync: (msg, time, retries, context) => Task.CompletedTask
            );
    }
}