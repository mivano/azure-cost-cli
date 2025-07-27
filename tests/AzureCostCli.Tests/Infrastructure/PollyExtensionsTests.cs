using AzureCostCli.Infrastructure;
using Shouldly;
using System.Net;
using Xunit;

namespace AzureCostCli.Tests.Infrastructure;

public class PollyExtensionsTests
{
    [Fact]
    public void GetRetryAfterPolicy_ReturnsPolicy()
    {
        // Act
        var policy = PollyExtensions.GetRetryAfterPolicy();

        // Assert
        policy.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetRetryAfterPolicy_HandlesSuccessfulResponse()
    {
        // Arrange
        var policy = PollyExtensions.GetRetryAfterPolicy();
        var response = new HttpResponseMessage(HttpStatusCode.OK);

        // Act
        var result = await policy.ExecuteAsync(() => Task.FromResult(response));

        // Assert
        result.ShouldBe(response);
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRetryAfterPolicy_HandlesNonRetryableErrors()
    {
        // Arrange
        var policy = PollyExtensions.GetRetryAfterPolicy();
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);

        // Act
        var result = await policy.ExecuteAsync(() => Task.FromResult(response));

        // Assert
        result.ShouldBe(response);
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}