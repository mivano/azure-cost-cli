using AzureCostCli.Commands;
using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.Commands.Budgets;
using AzureCostCli.CostApi;
using Shouldly;
using Moq;
using Spectre.Console.Cli;
using Xunit;

namespace AzureCostCli.Tests.Commands;

public class AccumulatedCostCommandTests
{
    private readonly Mock<ICostRetriever> _mockCostRetriever;
    private readonly AccumulatedCostCommand _command;

    public AccumulatedCostCommandTests()
    {
        _mockCostRetriever = new Mock<ICostRetriever>();
        _command = new AccumulatedCostCommand(_mockCostRetriever.Object);
    }

    [Fact]
    public void Validate_WithCustomTimeframeAndValidDates_ReturnsSuccess()
    {
        // Arrange
        var settings = new AccumulatedCostSettings
        {
            Subscription = Guid.NewGuid(),
            Timeframe = TimeframeType.Custom,
            From = new DateOnly(2023, 1, 1),
            To = new DateOnly(2023, 1, 31)
        };
        var context = CreateCommandContext();

        // Act
        var result = _command.Validate(context, settings);

        // Assert
        result.Successful.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithCustomTimeframeAndFromDateAfterToDate_ReturnsError()
    {
        // Arrange
        var settings = new AccumulatedCostSettings
        {
            Subscription = Guid.NewGuid(),
            Timeframe = TimeframeType.Custom,
            From = new DateOnly(2023, 1, 31),
            To = new DateOnly(2023, 1, 1)
        };
        var context = CreateCommandContext();

        // Act
        var result = _command.Validate(context, settings);

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldBe("The from date must be before the to date.");
    }

    [Fact]
    public void Validate_WithNonCustomTimeframe_ReturnsSuccess()
    {
        // Arrange
        var settings = new AccumulatedCostSettings
        {
            Subscription = Guid.NewGuid(),
            Timeframe = TimeframeType.MonthToDate
        };
        var context = CreateCommandContext();

        // Act
        var result = _command.Validate(context, settings);

        // Assert
        result.Successful.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_SetsUpOutputFormatters()
    {
        // Act & Assert - Constructor should not throw
        var command = new AccumulatedCostCommand(_mockCostRetriever.Object);
        command.ShouldNotBeNull();
    }

    [Fact]
    public void Validate_WithNoSubscriptionAndNoAzureCLI_ReturnsError()
    {
        // Arrange
        var settings = new AccumulatedCostSettings
        {
            Subscription = null // No subscription provided
        };
        var context = CreateCommandContext();

        // Act
        var result = _command.Validate(context, settings);

        // Assert
        result.Successful.ShouldBeFalse();
        result.Message.ShouldContain("No subscription ID provided");
        result.Message.ShouldContain("--help");
    }

    [Fact]
    public void Validate_WithSubscription_ReturnsSuccess()
    {
        // Arrange
        var settings = new AccumulatedCostSettings
        {
            Subscription = Guid.NewGuid(),
            Timeframe = TimeframeType.BillingMonthToDate
        };
        var context = CreateCommandContext();

        // Act
        var result = _command.Validate(context, settings);

        // Assert
        result.Successful.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithBillingAccountAndNoSubscription_ReturnsSuccess()
    {
        // Arrange
        var settings = new AccumulatedCostSettings
        {
            Subscription = null,
            BillingAccountId = "12345",
            Timeframe = TimeframeType.BillingMonthToDate
        };
        var context = CreateCommandContext();

        // Act
        var result = _command.Validate(context, settings);

        // Assert
        result.Successful.ShouldBeTrue();
    }

    private static CommandContext CreateCommandContext()
    {
        var remainingArguments = Mock.Of<IRemainingArguments>();
        return new CommandContext([], remainingArguments, "accumulated-cost", null);
    }
}

public class BudgetsCommandTests
{
    private readonly Mock<ICostRetriever> _mockCostRetriever;
    private readonly BudgetsCommand _command;

    public BudgetsCommandTests()
    {
        _mockCostRetriever = new Mock<ICostRetriever>();
        _command = new BudgetsCommand(_mockCostRetriever.Object);
    }

    [Fact]
    public void Constructor_SetsUpOutputFormatters()
    {
        // Act & Assert - Constructor should not throw
        var command = new BudgetsCommand(_mockCostRetriever.Object);
        command.ShouldNotBeNull();
    }

    [Fact]
    public void BudgetsSettings_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var settings = new BudgetsSettings();

        // Assert
        settings.Output.ShouldBe(OutputFormat.Console);
        settings.UseUSD.ShouldBeFalse();
    }
}