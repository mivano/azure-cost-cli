using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.Commands.Budgets;
using AzureCostCli.Commands.DailyCost;
using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Shouldly;
using System.Text.Json;
using CsvHelper;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace AzureCostCli.Tests.OutputFormatters;

[Collection("ConsoleOutputTests")]
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
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
            var settings = new DailyCostSettings { IncludeTags = false };
            var dailyCosts = new List<CostDailyItem>
            {
                new(new DateOnly(2023, 1, 15), "Test Resource", 100.0, 105.0, "USD", null),
                new(new DateOnly(2023, 1, 16), "Another Resource", 50.0, 52.5, "USD", null)
            };

            // Act
            await _formatter.WriteDailyCost(settings, dailyCosts);
            var csvOutput = output.ToString();

            // Assert - Validate CSV can be parsed with current culture
            using var reader = new StringReader(csvOutput);
            using var csv = new CsvReader(reader, CultureInfo.CurrentCulture);
            
            var records = csv.GetRecords<dynamic>().ToList();
            records.Count.ShouldBe(2);
            
            // Validate header exists (culture-agnostic check)
            csvOutput.ShouldContain("Date");
            csvOutput.ShouldContain("Name");
            csvOutput.ShouldContain("Cost");
            csvOutput.ShouldContain("Currency");
            
            // Validate data exists (culture-tolerant checks)
            csvOutput.ShouldContain("Test Resource");
            csvOutput.ShouldContain("Another Resource");
            csvOutput.ShouldContain("100");
            csvOutput.ShouldContain("50");
            csvOutput.ShouldContain("USD");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task WriteCostByResource_ProducesValidCsvOutput()
    {
        // Arrange
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
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

            // Assert - Validate CSV structure and content (culture-tolerant)
            csvOutput.ShouldContain("Cost");
            csvOutput.ShouldContain("CostUSD");
            csvOutput.ShouldContain("ResourceId");
            csvOutput.ShouldContain("ResourceType");
            csvOutput.ShouldContain("ResourceLocation");
            
            // Check for numeric values (flexible format)
            csvOutput.ShouldContain("100");
            csvOutput.ShouldContain("105");
            csvOutput.ShouldContain("Microsoft.Compute/virtualMachines");
            csvOutput.ShouldContain("East US");
            
            // Validate it's parseable CSV with current culture
            using var reader = new StringReader(csvOutput);
            using var csv = new CsvReader(reader, CultureInfo.CurrentCulture);
            var records = csv.GetRecords<dynamic>().ToList();
            records.Count.ShouldBe(1);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}

[Collection("ConsoleOutputTests")]
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
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
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
            // Check for 1000 or 1,000 or 1.000 (culture-invariant amount check)
            Regex.IsMatch(textOutput, @"1[.,]?000").ShouldBeTrue("Should contain 1000 in some culture format");
            textOutput.ShouldContain("Monthly");
            textOutput.ShouldContain("2023");  // Check for the year without specific date format
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task WriteCostByResource_ProducesReadableTextOutput()
    {
        // Arrange
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
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
            textOutput.ShouldContain("100");  // Check for the number without specific formatting
            textOutput.ShouldContain("USD");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task WriteAccumulatedCost_WithEmptyCosts_ShouldNotThrow()
    {
        // Arrange
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
            var settings = new AzureCostCli.Commands.AccumulatedCost.AccumulatedCostSettings();
            var subscription = new Subscription("123", "Test Subscription", new object[0], "Test", "Test", "Test Subscription", "Active", new SubscriptionPolicies("", "", ""));
            var accumulatedCostDetails = new AccumulatedCostDetails(
                subscription,
                null,
                new List<CostItem>(), // Empty costs list
                new List<CostItem>(),
                new List<CostNamedItem>(),
                new List<CostNamedItem>(),
                new List<CostNamedItem>(),
                null
            );

            // Act & Assert - Should not throw
            await _formatter.WriteAccumulatedCost(settings, accumulatedCostDetails);
            var textOutput = output.ToString();

            // Assert - Validate it shows a "No data found" message
            textOutput.ShouldContain("Azure Cost Overview");
            textOutput.ShouldContain("No data found");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}

[Collection("ConsoleOutputTests")]
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
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
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
            // Check for 1000 or 1,000 or 1.000 (culture-invariant amount check)
            Regex.IsMatch(markdownOutput, @"1[.,]?000").ShouldBeTrue("Should contain 1000 in some culture format");
            markdownOutput.ShouldContain("Monthly");
            markdownOutput.ShouldContain("<sup>Generated at");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task WriteCostByResource_ProducesValidMarkdownOutput()
    {
        // Arrange
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
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
            markdownOutput.ShouldContain("100");  // Check for the number without specific formatting
            markdownOutput.ShouldContain("USD");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task WriteAccumulatedCost_WithEmptyCosts_ShouldNotThrow()
    {
        // Arrange
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
            var settings = new AzureCostCli.Commands.AccumulatedCost.AccumulatedCostSettings();
            var subscription = new Subscription("123", "Test Subscription", new object[0], "Test", "Test", "Test Subscription", "Active", new SubscriptionPolicies("", "", ""));
            var accumulatedCostDetails = new AccumulatedCostDetails(
                subscription,
                null,
                new List<CostItem>(), // Empty costs list
                new List<CostItem>(),
                new List<CostNamedItem>(),
                new List<CostNamedItem>(),
                new List<CostNamedItem>(),
                null
            );

            // Act & Assert - Should not throw
            await _formatter.WriteAccumulatedCost(settings, accumulatedCostDetails);
            var markdownOutput = output.ToString();

            // Assert - Validate it shows a "No data found" message
            markdownOutput.ShouldContain("# Azure Cost Overview");
            markdownOutput.ShouldContain("No data found");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task WriteAccumulatedCost_WithOnlyForecastedCosts_ShouldShowForecasts()
    {
        // Arrange
        var originalOut = Console.Out;
        var output = new StringWriter();
        Console.SetOut(output);
        
        try
        {
            var settings = new AzureCostCli.Commands.AccumulatedCost.AccumulatedCostSettings();
            var subscription = new Subscription("123", "Test Subscription", new object[0], "Test", "Test", "Test Subscription", "Active", new SubscriptionPolicies("", "", ""));
            var forecastedCosts = new List<CostItem>
            {
                new CostItem(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), 50.0, 50.0, "USD"),
                new CostItem(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)), 60.0, 60.0, "USD")
            };
            var accumulatedCostDetails = new AccumulatedCostDetails(
                subscription,
                null,
                new List<CostItem>(), // Empty costs list
                forecastedCosts, // But has forecasted costs
                new List<CostNamedItem>(),
                new List<CostNamedItem>(),
                new List<CostNamedItem>(),
                null
            );

            // Act & Assert - Should not throw
            await _formatter.WriteAccumulatedCost(settings, accumulatedCostDetails);
            var markdownOutput = output.ToString();

            // Assert - Validate it shows forecasted costs
            markdownOutput.ShouldContain("# Azure Cost Overview");
            markdownOutput.ShouldContain("Forecasted cost");
            markdownOutput.ShouldContain("No historical cost data available");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}