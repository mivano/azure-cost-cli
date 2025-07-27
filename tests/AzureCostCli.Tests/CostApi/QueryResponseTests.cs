using AzureCostCli.CostApi;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace AzureCostCli.Tests.CostApi;

public class QueryResponseTests
{
    [Fact]
    public void Combine_WithValidOtherResponse_CombinesRows()
    {
        // Arrange
        var response1 = new QueryResponse
        {
            properties = new Properties
            {
                rows = new List<JsonElement>
                {
                    JsonDocument.Parse("\"row1\"").RootElement,
                    JsonDocument.Parse("\"row2\"").RootElement
                }
            }
        };

        var response2 = new QueryResponse
        {
            properties = new Properties
            {
                rows = new List<JsonElement>
                {
                    JsonDocument.Parse("\"row3\"").RootElement,
                    JsonDocument.Parse("\"row4\"").RootElement
                }
            }
        };

        // Act
        response1.Combine(response2);

        // Assert
        response1.properties.rows.Should().HaveCount(4);
        response1.properties.rows[0].GetString().Should().Be("row1");
        response1.properties.rows[1].GetString().Should().Be("row2");
        response1.properties.rows[2].GetString().Should().Be("row3");
        response1.properties.rows[3].GetString().Should().Be("row4");
    }

    [Fact]
    public void Combine_WithNullOther_DoesNotThrow()
    {
        // Arrange
        var response = new QueryResponse
        {
            properties = new Properties
            {
                rows = new List<JsonElement>
                {
                    JsonDocument.Parse("\"row1\"").RootElement
                }
            }
        };

        // Act & Assert - Should not throw
        response.Combine(null);
        response.properties.rows.Should().HaveCount(1);
    }

    [Fact]
    public void Combine_WithOtherHavingNullProperties_DoesNotThrow()
    {
        // Arrange
        var response1 = new QueryResponse
        {
            properties = new Properties
            {
                rows = new List<JsonElement>
                {
                    JsonDocument.Parse("\"row1\"").RootElement
                }
            }
        };

        var response2 = new QueryResponse
        {
            properties = null
        };

        // Act & Assert - Should not throw
        response1.Combine(response2);
        response1.properties.rows.Should().HaveCount(1);
    }

    [Fact]
    public void Combine_WithOtherHavingNullRows_DoesNotThrow()
    {
        // Arrange
        var response1 = new QueryResponse
        {
            properties = new Properties
            {
                rows = new List<JsonElement>
                {
                    JsonDocument.Parse("\"row1\"").RootElement
                }
            }
        };

        var response2 = new QueryResponse
        {
            properties = new Properties
            {
                rows = null
            }
        };

        // Act & Assert - Should not throw
        response1.Combine(response2);
        response1.properties.rows.Should().HaveCount(1);
    }

    [Fact]
    public void Combine_WithEmptyRows_CombinesSuccessfully()
    {
        // Arrange
        var response1 = new QueryResponse
        {
            properties = new Properties
            {
                rows = new List<JsonElement>
                {
                    JsonDocument.Parse("\"row1\"").RootElement
                }
            }
        };

        var response2 = new QueryResponse
        {
            properties = new Properties
            {
                rows = new List<JsonElement>()
            }
        };

        // Act
        response1.Combine(response2);

        // Assert
        response1.properties.rows.Should().HaveCount(1);
    }

    [Fact]
    public void QueryResponse_CanBeCreated_WithAllProperties()
    {
        // Arrange & Act
        var response = new QueryResponse
        {
            eTag = "test-etag",
            id = "test-id",
            location = "East US",
            name = "test-name",
            type = "test-type",
            properties = new Properties
            {
                columns = new[]
                {
                    new Columns { name = "Date", type = "string" },
                    new Columns { name = "Cost", type = "number" }
                },
                nextLink = "https://next-link",
                rows = new List<JsonElement>()
            }
        };

        // Assert
        response.eTag.Should().Be("test-etag");
        response.id.Should().Be("test-id");
        response.location.Should().Be("East US");
        response.name.Should().Be("test-name");
        response.type.Should().Be("test-type");
        response.properties.Should().NotBeNull();
        response.properties.columns.Should().HaveCount(2);
        response.properties.columns[0].name.Should().Be("Date");
        response.properties.columns[0].type.Should().Be("string");
        response.properties.columns[1].name.Should().Be("Cost");
        response.properties.columns[1].type.Should().Be("number");
        response.properties.nextLink.Should().Be("https://next-link");
        response.properties.rows.Should().BeEmpty();
    }
}