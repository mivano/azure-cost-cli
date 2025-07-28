using AzureCostCli.Commands.Budgets;
using AzureCostCli.Commands.DailyCost;
using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Shouldly;
using System.Text.Json;
using CsvHelper;
using System.Globalization;
using Xunit;

namespace AzureCostCli.Tests.OutputFormatters;

public class CsvOutputFormatterTests
{
    private readonly CsvOutputFormatter _formatter;

    public CsvOutputFormatterTests()
    {
        _formatter = new CsvOutputFormatter();
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
    public async Task WriteDailyCost_WithoutTags_ShouldNotThrow()
    {
        // Arrange
        var settings = new DailyCostSettings { IncludeTags = false };
        var dailyCosts = new List<CostDailyItem>
        {
            new(new DateOnly(2023, 1, 15), "Test Resource", 100.0, 105.0, "USD", null)
        };

        // Act & Assert - Should not throw
        await _formatter.WriteDailyCost(settings, dailyCosts);
    }

    [Fact]
    public async Task WriteDailyCost_WithTags_ShouldNotThrow()
    {
        // Arrange
        var settings = new DailyCostSettings { IncludeTags = true };
        var tags = new Dictionary<string, string> { ["Environment"] = "Production", ["Team"] = "Backend" };
        var dailyCosts = new List<CostDailyItem>
        {
            new(new DateOnly(2023, 1, 15), "Test Resource", 100.0, 105.0, "USD", tags)
        };

        // Act & Assert - Should not throw
        await _formatter.WriteDailyCost(settings, dailyCosts);
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
    public async Task WriteDailyCost_ProducesValidCsvOutput()
    {
        // Arrange
        var output = new StringWriter();
        Console.SetOut(output);
        
        var settings = new DailyCostSettings { IncludeTags = false };
        var dailyCosts = new List<CostDailyItem>
        {
            new(new DateOnly(2023, 1, 15), "Test Resource", 100.0, 105.0, "USD", null),
            new(new DateOnly(2023, 1, 16), "Another Resource", 50.0, 52.5, "USD", null)
        };

        // Act
        await _formatter.WriteDailyCost(settings, dailyCosts);
        var csvOutput = output.ToString();

        // Assert - Validate CSV can be parsed
        using var reader = new StringReader(csvOutput);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        
        var records = csv.GetRecords<dynamic>().ToList();
        records.Count.ShouldBe(2);
        
        // Validate header exists and output structure
        csvOutput.ShouldContain("Date,Name,Cost,CostUsd,Currency");
        csvOutput.ShouldContain("01/15/2023,Test Resource,100");
        csvOutput.ShouldContain("01/16/2023,Another Resource,50");
    }

    [Fact]
    public async Task WriteCostByResource_ProducesValidCsvOutput()
    {
        // Arrange
        var output = new StringWriter();
        Console.SetOut(output);
        
        var settings = new AzureCostCli.Commands.CostByResource.CostByResourceSettings();
        var resources = new List<CostResourceItem>
        {
            new(100.0, 105.0, "/subscriptions/123/resourceGroups/test/providers/Microsoft.Compute/virtualMachines/test-vm", 
                "Microsoft.Compute/virtualMachines", "East US", "Usage", "test-rg", "Microsoft", 
                "Virtual Machines", "Standard", "D2s v3", new Dictionary<string, string>(), "USD")
        };

        // Act
        await _formatter.WriteCostByResource(settings, resources);
        var csvOutput = output.ToString();

        // Assert - Validate CSV structure and content
        csvOutput.ShouldContain("Cost,CostUSD,ResourceId,ResourceType,ResourceLocation");
        csvOutput.ShouldContain("100.00000000,105.00000000");
        csvOutput.ShouldContain("Microsoft.Compute/virtualMachines");
        csvOutput.ShouldContain("East US");
        
        // Validate it's parseable CSV
        using var reader = new StringReader(csvOutput);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<dynamic>().ToList();
        records.Count.ShouldBe(1);
    }
}

public class TextOutputFormatterTests
{
    private readonly TextOutputFormatter _formatter;

    public TextOutputFormatterTests()
    {
        _formatter = new TextOutputFormatter();
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
    public async Task WriteBudgets_ProducesReadableTextOutput()
    {
        // Arrange
        var output = new StringWriter();
        Console.SetOut(output);
        
        var settings = new BudgetsSettings();
        var budgets = new List<BudgetItem>
        {
            new("Test Budget", "/subscriptions/123/budgets/test", 1000.0, "Monthly", 
                new DateTime(2023, 1, 1), new DateTime(2023, 12, 31), 
                250.0, "USD", 800.0, "USD", new List<Notification>())
        };

        // Act
        await _formatter.WriteBudgets(settings, budgets);
        var textOutput = output.ToString();

        // Assert - Validate readable text format
        textOutput.ShouldContain("Azure Budgets");
        textOutput.ShouldContain("Test Budget");
        textOutput.ShouldContain("1,000.00");
        textOutput.ShouldContain("Monthly");
        textOutput.ShouldContain("01/01/2023");
        textOutput.ShouldContain("12/31/2023");
    }

    [Fact]
    public async Task WriteCostByResource_ProducesReadableTextOutput()
    {
        // Arrange
        var output = new StringWriter();
        Console.SetOut(output);
        
        var settings = new AzureCostCli.Commands.CostByResource.CostByResourceSettings();
        var resources = new List<CostResourceItem>
        {
            new(100.0, 105.0, "/subscriptions/123/resourceGroups/test/providers/Microsoft.Compute/virtualMachines/test-vm", 
                "Microsoft.Compute/virtualMachines", "East US", "Usage", "test-rg", "Microsoft", 
                "Virtual Machines", "Standard", "D2s v3", new Dictionary<string, string>(), "USD")
        };

        // Act
        await _formatter.WriteCostByResource(settings, resources);
        var textOutput = output.ToString();

        // Assert - Validate readable text format
        textOutput.ShouldContain("Azure Cost Overview");
        textOutput.ShouldContain("test-vm");
        textOutput.ShouldContain("Microsoft.Compute/virtualMachines");
        textOutput.ShouldContain("East US");
        textOutput.ShouldContain("test-rg");
        textOutput.ShouldContain("100.00 USD");
    }
}

public class MarkdownOutputFormatterTests
{
    private readonly MarkdownOutputFormatter _formatter;

    public MarkdownOutputFormatterTests()
    {
        _formatter = new MarkdownOutputFormatter();
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
    public async Task WriteBudgets_ProducesValidMarkdownOutput()
    {
        // Arrange
        var output = new StringWriter();
        Console.SetOut(output);
        
        var settings = new BudgetsSettings();
        var budgets = new List<BudgetItem>
        {
            new("Test Budget", "/subscriptions/123/budgets/test", 1000.0, "Monthly", 
                new DateTime(2023, 1, 1), new DateTime(2023, 12, 31), 
                250.0, "USD", 800.0, "USD", new List<Notification>())
        };

        // Act
        await _formatter.WriteBudgets(settings, budgets);
        var markdownOutput = output.ToString();

        // Assert - Validate markdown structure
        markdownOutput.ShouldContain("# Azure Budgets");
        markdownOutput.ShouldContain("## Budget `Test Budget`");
        markdownOutput.ShouldContain("1,000.00");
        markdownOutput.ShouldContain("Monthly");
        markdownOutput.ShouldContain("<sup>Generated at");
    }

    [Fact]
    public async Task WriteCostByResource_ProducesValidMarkdownOutput()
    {
        // Arrange
        var output = new StringWriter();
        Console.SetOut(output);
        
        var settings = new AzureCostCli.Commands.CostByResource.CostByResourceSettings();
        var resources = new List<CostResourceItem>
        {
            new(100.0, 105.0, "/subscriptions/123/resourceGroups/test/providers/Microsoft.Compute/virtualMachines/test-vm", 
                "Microsoft.Compute/virtualMachines", "East US", "Usage", "test-rg", "Microsoft", 
                "Virtual Machines", "Standard", "D2s v3", new Dictionary<string, string>(), "USD")
        };

        // Act
        await _formatter.WriteCostByResource(settings, resources);
        var markdownOutput = output.ToString();

        // Assert - Validate markdown table structure
        markdownOutput.ShouldContain("# Azure Cost by Resource");
        markdownOutput.ShouldContain("| ResourceName | ResourceType | Location | ResourceGroupName |");
        markdownOutput.ShouldContain("|---|---|---|---|");
        markdownOutput.ShouldContain("|test-vm | Microsoft.Compute/virtualMachines | East US | test-rg |");
        markdownOutput.ShouldContain("100.00 USD");
    }
}