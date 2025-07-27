using AzureCostCli.Commands.Budgets;
using AzureCostCli.Commands.DailyCost;
using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using FluentAssertions;
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
}