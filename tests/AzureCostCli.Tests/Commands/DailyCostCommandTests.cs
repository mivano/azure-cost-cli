using AzureCostCli.Commands;
using AzureCostCli.Commands.DailyCost;
using AzureCostCli.CostApi;
using Shouldly;
using Moq;
using Spectre.Console.Cli;
using Xunit;

namespace AzureCostCli.Tests.Commands;

public class DailyCostCommandTests
{
    private readonly Mock<ICostRetriever> _mockCostRetriever;
    private readonly DailyCostCommand _command;

    public DailyCostCommandTests()
    {
        _mockCostRetriever = new Mock<ICostRetriever>();
        _command = new DailyCostCommand(_mockCostRetriever.Object);
    }

    [Fact]
    public void Validate_WithCustomTimeframeAndValidDates_ReturnsSuccess()
    {
        // Arrange
        var settings = new DailyCostSettings
        {
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
        var settings = new DailyCostSettings
        {
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
        var settings = new DailyCostSettings
        {
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
        var command = new DailyCostCommand(_mockCostRetriever.Object);
        command.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(TimeframeType.BillingMonthToDate)]
    [InlineData(TimeframeType.MonthToDate)]
    [InlineData(TimeframeType.TheLastBillingMonth)]
    [InlineData(TimeframeType.TheLastMonth)]
    [InlineData(TimeframeType.WeekToDate)]
    public void Validate_WithNonCustomTimeframeTypes_ReturnsSuccess(TimeframeType timeframe)
    {
        // Arrange
        var settings = new DailyCostSettings
        {
            Timeframe = timeframe
        };
        var context = CreateCommandContext();

        // Act
        var result = _command.Validate(context, settings);

        // Assert
        result.Successful.ShouldBeTrue();
    }

    [Fact]
    public void DailyCostSettings_DefaultDimension_ShouldBeResourceGroupName()
    {
        // Arrange & Act
        var settings = new DailyCostSettings();

        // Assert
        settings.Dimension.ShouldBe("ResourceGroupName");
    }

    private static CommandContext CreateCommandContext()
    {
        // Use reflection to create CommandContext since constructor parameters are complex
        var remainingArguments = Mock.Of<IRemainingArguments>();
        return new CommandContext([], remainingArguments, "daily-cost", null);
    }
}