using AzureCostCli.Commands;
using AzureCostCli.Commands.CostByTag;
using AzureCostCli.Commands.DetectAnomaly;
using AzureCostCli.Commands.Regions;
using AzureCostCli.Commands.Prices;
using AzureCostCli.CostApi;
using Shouldly;
using Moq;
using Spectre.Console.Cli;
using Xunit;

namespace AzureCostCli.Tests.Commands;

public class CostByTagCommandTests
{
    private readonly Mock<ICostRetriever> _mockCostRetriever;
    private readonly CostByTagCommand _command;

    public CostByTagCommandTests()
    {
        _mockCostRetriever = new Mock<ICostRetriever>();
        _command = new CostByTagCommand(_mockCostRetriever.Object);
    }

    [Fact]
    public void Validate_WithCustomTimeframeAndValidDates_ReturnsSuccess()
    {
        // Arrange
        var settings = new CostByTagSettings
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
        var settings = new CostByTagSettings
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
        var settings = new CostByTagSettings
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
        var command = new CostByTagCommand(_mockCostRetriever.Object);
        command.ShouldNotBeNull();
    }

    private static CommandContext CreateCommandContext()
    {
        var remainingArguments = Mock.Of<IRemainingArguments>();
        return new CommandContext([], remainingArguments, "cost-by-tag", null);
    }
}

public class DetectAnomalyCommandTests
{
    private readonly Mock<ICostRetriever> _mockCostRetriever;
    private readonly DetectAnomalyCommand _command;

    public DetectAnomalyCommandTests()
    {
        _mockCostRetriever = new Mock<ICostRetriever>();
        _command = new DetectAnomalyCommand(_mockCostRetriever.Object);
    }

    [Fact]
    public void Validate_WithCustomTimeframeAndValidDates_ReturnsSuccess()
    {
        // Arrange
        var settings = new DetectAnomalySettings
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
        var settings = new DetectAnomalySettings
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
    public void Constructor_SetsUpOutputFormatters()
    {
        // Act & Assert - Constructor should not throw
        var command = new DetectAnomalyCommand(_mockCostRetriever.Object);
        command.ShouldNotBeNull();
    }

    private static CommandContext CreateCommandContext()
    {
        var remainingArguments = Mock.Of<IRemainingArguments>();
        return new CommandContext([], remainingArguments, "detect-anomaly", null);
    }
}

public class RegionsCommandTests
{
    private readonly Mock<IRegionsRetriever> _mockRegionsRetriever;
    private readonly RegionsCommand _command;

    public RegionsCommandTests()
    {
        _mockRegionsRetriever = new Mock<IRegionsRetriever>();
        _command = new RegionsCommand(_mockRegionsRetriever.Object);
    }

    [Fact]
    public void Constructor_SetsUpOutputFormatters()
    {
        // Act & Assert - Constructor should not throw
        var command = new RegionsCommand(_mockRegionsRetriever.Object);
        command.ShouldNotBeNull();
    }

    [Fact]
    public void RegionsSettings_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var settings = new RegionsSettings();

        // Assert
        settings.Output.ShouldBe(OutputFormat.Console);
    }

    private static CommandContext CreateCommandContext()
    {
        var remainingArguments = Mock.Of<IRemainingArguments>();
        return new CommandContext([], remainingArguments, "regions", null);
    }
}

public class ListPricesCommandTests
{
    private readonly Mock<IPriceRetriever> _mockPriceRetriever;
    private readonly ListPricesCommand _command;

    public ListPricesCommandTests()
    {
        _mockPriceRetriever = new Mock<IPriceRetriever>();
        _command = new ListPricesCommand(_mockPriceRetriever.Object);
    }

    [Fact]
    public void Constructor_SetsUpOutputFormatters()
    {
        // Act & Assert - Constructor should not throw
        var command = new ListPricesCommand(_mockPriceRetriever.Object);
        command.ShouldNotBeNull();
    }

    [Fact]
    public void PricesSettings_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var settings = new PricesSettings();

        // Assert
        settings.Output.ShouldBe(OutputFormat.Console);
    }

    private static CommandContext CreateCommandContext()
    {
        var remainingArguments = Mock.Of<IRemainingArguments>();
        return new CommandContext([], remainingArguments, "prices", null);
    }
}