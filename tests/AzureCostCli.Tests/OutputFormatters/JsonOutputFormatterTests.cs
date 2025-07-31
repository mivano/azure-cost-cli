using AzureCostCli.Commands;
using AzureCostCli.Commands.Budgets;
using AzureCostCli.Commands.CostByResource;
using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace AzureCostCli.Tests.OutputFormatters;

[Collection("ConsoleOutputTests")]
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
        var settings = new BudgetsSettings { Output = OutputFormat.Json };
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
        var settings = new AzureCostCli.Commands.CostByResource.CostByResourceSettings { Output = OutputFormat.Json };
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
    public async Task WriteBudgets_ProducesValidJsonOutput()
    {
        // Arrange
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
            var settings = new BudgetsSettings { Output = OutputFormat.Json };
            var budgets = new List<BudgetItem>
            {
                new("Test Budget", "/subscriptions/123/budgets/test", 1000.0, "Monthly", 
                    new DateTime(2023, 1, 1), new DateTime(2023, 12, 31), 
                    250.0, "USD", 800.0, "USD", new List<Notification>())
            };

            // Act
            await _formatter.WriteBudgets(settings, budgets);
            var jsonOutput = output.ToString();

            // Assert - Validate JSON can be parsed and contains expected data
            var parsedJson = JsonDocument.Parse(jsonOutput);
            var budgetArray = parsedJson.RootElement;
            budgetArray.ValueKind.ShouldBe(JsonValueKind.Array);
            budgetArray.GetArrayLength().ShouldBe(1);
            
            var budget = budgetArray[0];
            budget.GetProperty("Name").GetString().ShouldBe("Test Budget");
            budget.GetProperty("Amount").GetDouble().ShouldBe(1000.0);
            budget.GetProperty("TimeGrain").GetString().ShouldBe("Monthly");
            budget.GetProperty("CurrentSpendAmount").GetDouble().ShouldBe(250.0);
            budget.GetProperty("CurrentSpendCurrency").GetString().ShouldBe("USD");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task WriteCostByResource_ProducesValidJsonOutput()
    {
        // Arrange
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
            var settings = new AzureCostCli.Commands.CostByResource.CostByResourceSettings { Output = OutputFormat.Json };
            var resources = new List<CostResourceItem>
            {
                new(100.0, 105.0, "/subscriptions/123/resourceGroups/test/providers/Microsoft.Compute/virtualMachines/test-vm", 
                    "Microsoft.Compute/virtualMachines", "East US", "Usage", "test-rg", "Microsoft", 
                    "Virtual Machines", "Standard", "D2s v3", new Dictionary<string, string>(), "USD")
            };

            // Act
            await _formatter.WriteCostByResource(settings, resources);
            var jsonOutput = output.ToString();

            // Assert - Validate JSON structure and content
            var parsedJson = JsonDocument.Parse(jsonOutput);
            var resourceArray = parsedJson.RootElement;
            resourceArray.ValueKind.ShouldBe(JsonValueKind.Array);
            resourceArray.GetArrayLength().ShouldBe(1);
            
            var resource = resourceArray[0];
            resource.GetProperty("Cost").GetDouble().ShouldBe(100.0);
            resource.GetProperty("CostUSD").GetDouble().ShouldBe(105.0);
            resource.GetProperty("ResourceType").GetString().ShouldBe("Microsoft.Compute/virtualMachines");
            resource.GetProperty("ResourceLocation").GetString().ShouldBe("East US");
            resource.GetProperty("ResourceGroupName").GetString().ShouldBe("test-rg");
            resource.GetProperty("ServiceName").GetString().ShouldBe("Virtual Machines");
            resource.GetProperty("Currency").GetString().ShouldBe("USD");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
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
        result.ShouldBe(new DateOnly(2023, 1, 15));
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
        json.ShouldBe("\"2023-01-15\"");
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
        resourceName.ShouldBe("my-test-vm");
    }

    [Fact]
    public async Task WriteCostByResource_WithJsoncFormat_ProducesColoredJsonOutput()
    {
        // Arrange
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
            var settings = new CostByResourceSettings { Output = OutputFormat.Jsonc };
            var resources = new List<CostResourceItem>
            {
                new(100.0, 105.0, "/subscriptions/123/resourceGroups/test/providers/Microsoft.Compute/virtualMachines/test-vm", 
                    "Microsoft.Compute/virtualMachines", "East US", "Usage", "test-rg", "Microsoft", 
                    "Virtual Machines", "Standard", "D2s v3", new Dictionary<string, string>(), "USD")
            };

            // Act
            await _formatter.WriteCostByResource(settings, resources);
            var outputText = output.ToString();

            // Assert - Should contain ANSI escape sequences for colors and not be empty
            outputText.ShouldNotBeEmpty();
            outputText.ShouldContain('\x1b'); // ANSI escape character indicates colored output
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task WriteCostByResource_WithUnsupportedFormat_ThrowsArgumentException()
    {
        // Arrange
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
            var settings = new CostByResourceSettings { Output = OutputFormat.Markdown }; // Unsupported format
            var resources = new List<CostResourceItem>
            {
                new(100.0, 105.0, "/subscriptions/123/test-vm", 
                    "Microsoft.Compute/virtualMachines", "East US", "Usage", "test-rg", "Microsoft", 
                    "Virtual Machines", "Standard", "D2s v3", new Dictionary<string, string>(), "USD")
            };

            // Act & Assert
            var exception = await Should.ThrowAsync<ArgumentException>(
                async () => await _formatter.WriteCostByResource(settings, resources));
            
            exception.Message.ShouldContain("JsonOutputFormatter does not support output format: Markdown");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
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
        resourceName.ShouldBe("simple-resource-name");
    }
}