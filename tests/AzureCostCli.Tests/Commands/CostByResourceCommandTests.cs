using AzureCostCli.Commands;
using AzureCostCli.Commands.CostByResource;
using AzureCostCli.CostApi;
using Shouldly;
using Moq;
using Spectre.Console.Cli;
using Xunit;

namespace AzureCostCli.Tests.Commands;

public class CostByResourceCommandTests
{
    private readonly Mock<ICostRetriever> _mockCostRetriever;
    private readonly CostByResourceCommand _command;

    public CostByResourceCommandTests()
    {
        _mockCostRetriever = new Mock<ICostRetriever>();
        _command = new CostByResourceCommand(_mockCostRetriever.Object);
    }

    [Fact]
    public void Validate_WithCustomTimeframeAndValidDates_ReturnsSuccess()
    {
        // Arrange
        var settings = new CostByResourceSettings
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
        var settings = new CostByResourceSettings
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
        var settings = new CostByResourceSettings
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
        var command = new CostByResourceCommand(_mockCostRetriever.Object);
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
        var settings = new CostByResourceSettings
        {
            Timeframe = timeframe
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
        return new CommandContext([], remainingArguments, "cost-by-resource", null);
    }
}