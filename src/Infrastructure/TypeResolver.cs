using System.Net;
using Polly;
using Spectre.Console.Cli;

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

public sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public object Resolve(Type type)
    {
        if (type == null)
        {
            return null;
        }

        return _provider.GetService(type);
    }

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}