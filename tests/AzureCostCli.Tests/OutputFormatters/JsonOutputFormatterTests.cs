using AzureCostCli.Commands.Budgets;
using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using FluentAssertions;
using Xunit;

namespace AzureCostCli.Tests.OutputFormatters;

public class JsonOutputFormatterTests
{
    private readonly JsonOutputFormatter _formatter;

    public JsonOutputFormatterTests()
    {
        _formatter = new JsonOutputFormatter();
    }

    [Fact]
    public async Task WriteBudgets_WithBudgets_ShouldNotThrow()
    {
        // Arrange
        var settings = new BudgetsSettings();
        var budgets = new List<BudgetItem>
        {
            new("Test Budget", "/subscriptions/123/budgets/test", 1000.0, "Monthly", 
                new DateTime(2023, 1, 1), new DateTime(2023, 12, 31), 
                250.0, "USD", 800.0, "USD", new List<Notification>())
        };

        // Act & Assert - Should not throw
        await _formatter.WriteBudgets(settings, budgets);
    }

    [Fact]
    public async Task WriteCostByResource_WithResources_ShouldNotThrow()
    {
        // Arrange
        var settings = new AzureCostCli.Commands.CostByResource.CostByResourceSettings();
        var resources = new List<CostResourceItem>
        {
            new(100.0, 105.0, "/subscriptions/123/resourceGroups/test/providers/Microsoft.Compute/virtualMachines/test-vm", 
                "Microsoft.Compute/virtualMachines", "East US", "Usage", "test-rg", "Microsoft", 
                "Virtual Machines", "Standard", "D2s v3", new Dictionary<string, string>(), "USD")
        };

        // Act & Assert - Should not throw
        await _formatter.WriteCostByResource(settings, resources);
    }

    [Fact]
    public void DateOnlyJsonConverter_Read_ParsesDateCorrectly()
    {
        // Arrange
        var converter = new DateOnlyJsonConverter();
        var json = "\"2023-01-15\"";
        var reader = new System.Text.Json.Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json));
        reader.Read(); // Move to the string value

        // Act
        var result = converter.Read(ref reader, typeof(DateOnly), new System.Text.Json.JsonSerializerOptions());

        // Assert
        result.Should().Be(new DateOnly(2023, 1, 15));
    }

    [Fact]
    public void DateOnlyJsonConverter_Write_WritesDateCorrectly()
    {
        // Arrange
        var converter = new DateOnlyJsonConverter();
        var date = new DateOnly(2023, 1, 15);
        using var stream = new MemoryStream();
        using var writer = new System.Text.Json.Utf8JsonWriter(stream);

        // Act
        converter.Write(writer, date, new System.Text.Json.JsonSerializerOptions());
        writer.Flush();

        // Assert
        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        json.Should().Be("\"2023-01-15\"");
    }

    [Fact]
    public void CostResourceItemExtensions_GetResourceName_ExtractsNameCorrectly()
    {
        // Arrange
        var resource = new CostResourceItem(
            100.0, 105.0, 
            "/subscriptions/12345/resourceGroups/test-rg/providers/Microsoft.Compute/virtualMachines/my-test-vm", 
            "Microsoft.Compute/virtualMachines", "East US", "Usage", "test-rg", "Microsoft", 
            "Virtual Machines", "Standard", "D2s v3", new Dictionary<string, string>(), "USD");

        // Act
        var resourceName = resource.GetResourceName();

        // Assert
        resourceName.Should().Be("my-test-vm");
    }

    [Fact]
    public void CostResourceItemExtensions_GetResourceName_WithSimpleId_ExtractsNameCorrectly()
    {
        // Arrange
        var resource = new CostResourceItem(
            50.0, 52.0, "simple-resource-name", 
            "Microsoft.Storage/storageAccounts", "West US", "Usage", "test-rg", "Microsoft", 
            "Storage", "Standard", "LRS", new Dictionary<string, string>(), "USD");

        // Act
        var resourceName = resource.GetResourceName();

        // Assert
        resourceName.Should().Be("simple-resource-name");
    }
}