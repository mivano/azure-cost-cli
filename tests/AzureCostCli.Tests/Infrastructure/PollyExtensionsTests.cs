using AzureCostCli.Infrastructure;
using FluentAssertions;
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
        policy.Should().NotBeNull();
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
        result.Should().Be(response);
        result.StatusCode.Should().Be(HttpStatusCode.OK);
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
        result.Should().Be(response);
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}