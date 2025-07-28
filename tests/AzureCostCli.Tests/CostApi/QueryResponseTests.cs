using AzureCostCli.CostApi;
using Shouldly;
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
        response1.properties.rows.Count.ShouldBe(4);
        response1.properties.rows[0].GetString().ShouldBe("row1");
        response1.properties.rows[1].GetString().ShouldBe("row2");
        response1.properties.rows[2].GetString().ShouldBe("row3");
        response1.properties.rows[3].GetString().ShouldBe("row4");
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
        response.properties.rows.Count.ShouldBe(1);
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
        response1.properties.rows.Count.ShouldBe(1);
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
        response1.properties.rows.Count.ShouldBe(1);
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
        response1.properties.rows.Count.ShouldBe(1);
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
        response.eTag.ShouldBe("test-etag");
        response.id.ShouldBe("test-id");
        response.location.ShouldBe("East US");
        response.name.ShouldBe("test-name");
        response.type.ShouldBe("test-type");
        response.properties.ShouldNotBeNull();
        response.properties.columns.Length.ShouldBe(2);
        response.properties.columns[0].name.ShouldBe("Date");
        response.properties.columns[0].type.ShouldBe("string");
        response.properties.columns[1].name.ShouldBe("Cost");
        response.properties.columns[1].type.ShouldBe("number");
        response.properties.nextLink.ShouldBe("https://next-link");
        response.properties.rows.ShouldBeEmpty();
    }
}