using AzureCostCli.Commands;
using AzureCostCli.Commands.Threshold;
using AzureCostCli.CostApi;
using Moq;
using Shouldly;
using Spectre.Console.Cli;
using Xunit;

namespace AzureCostCli.Tests.Commands;

[Collection("ConsoleOutputTests")]
public class ThresholdCommandTests
{
    private readonly Mock<ICostRetriever> _mockRetriever;

    public ThresholdCommandTests()
    {
        _mockRetriever = new Mock<ICostRetriever>();
        _mockRetriever.SetupProperty(r => r.CostApiAddress, "https://management.azure.com/");
        _mockRetriever.SetupProperty(r => r.HttpTimeout, TimeSpan.FromSeconds(100));
    }

    // -----------------------------------------------------------------------
    // ThresholdResult record tests
    // -----------------------------------------------------------------------

    [Fact]
    public void ThresholdResult_Exceeded_HasCorrectProperties()
    {
        var result = new ThresholdResult("daily-change", true, 25.5, 20.0, "Exceeded!");
        result.SubCommand.ShouldBe("daily-change");
        result.IsThresholdExceeded.ShouldBeTrue();
        result.ActualValue.ShouldBe(25.5);
        result.ThresholdValue.ShouldBe(20.0);
        result.Message.ShouldBe("Exceeded!");
    }

    [Fact]
    public void ThresholdResult_NotExceeded_HasCorrectProperties()
    {
        var result = new ThresholdResult("weekly-average", false, 10.0, 50.0, "OK");
        result.IsThresholdExceeded.ShouldBeFalse();
    }

    // -----------------------------------------------------------------------
    // ThresholdSettings validation tests
    // -----------------------------------------------------------------------

    [Fact]
    public void DailyChangeThresholdCommand_Validate_NoThreshold_ReturnsError()
    {
        var command = new DailyChangeThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            // Neither Percentage nor FixedAmount set
        };
        var ctx = CreateCommandContext();
        var result = ValidateHelper.CallValidate(command, ctx, settings);
        result.Successful.ShouldBeFalse();
        result.Message.ShouldContain("threshold");
    }

    [Fact]
    public void DailyChangeThresholdCommand_Validate_WithPercentage_ReturnsSuccess()
    {
        var command = new DailyChangeThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            Percentage = 20.0,
        };
        var ctx = CreateCommandContext();
        var result = ValidateHelper.CallValidate(command, ctx, settings);
        result.Successful.ShouldBeTrue();
    }

    [Fact]
    public void DailyChangeThresholdCommand_Validate_WithFixedAmount_ReturnsSuccess()
    {
        var command = new DailyChangeThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            FixedAmount = 100.0,
        };
        var ctx = CreateCommandContext();
        var result = ValidateHelper.CallValidate(command, ctx, settings);
        result.Successful.ShouldBeTrue();
    }

    [Fact]
    public void DailyChangeThresholdCommand_Validate_CustomTimeframeFromAfterTo_ReturnsError()
    {
        var command = new DailyChangeThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            Percentage = 20.0,
            Timeframe = TimeframeType.Custom,
            From = new DateOnly(2024, 6, 1),
            To = new DateOnly(2024, 1, 1),
        };
        var ctx = CreateCommandContext();
        var result = ValidateHelper.CallValidate(command, ctx, settings);
        result.Successful.ShouldBeFalse();
        result.Message.ShouldContain("from date must be before the to date");
    }

    // -----------------------------------------------------------------------
    // DailyChangeThresholdCommand — Execute logic tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DailyChange_WhenCostUnchanged_NotExceeded()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        _mockRetriever
            .Setup(r => r.RetrieveCosts(
                It.IsAny<bool>(), It.IsAny<Scope>(), It.IsAny<string[]>(),
                It.IsAny<MetricType>(), It.IsAny<TimeframeType>(),
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<CostItem>
            {
                new(today,    100.0, 100.0, "USD"),
                new(yesterday, 100.0, 100.0, "USD"),
            });

        var command = new DailyChangeThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            Percentage = 20.0,
            Output = OutputFormat.Text,
        };

        var exitCode = await InvokeExecuteAsync(command, settings);
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task DailyChange_WhenCostSpikes_ExceedsThreshold_FailOnThresholdTrue_Returns1()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        _mockRetriever
            .Setup(r => r.RetrieveCosts(
                It.IsAny<bool>(), It.IsAny<Scope>(), It.IsAny<string[]>(),
                It.IsAny<MetricType>(), It.IsAny<TimeframeType>(),
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<CostItem>
            {
                new(today,    200.0, 200.0, "USD"),
                new(yesterday, 100.0, 100.0, "USD"),
            });

        var command = new DailyChangeThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            Percentage = 20.0,   // 100% change > 20% threshold
            FailOnThreshold = true,
            Output = OutputFormat.Text,
        };

        var exitCode = await InvokeExecuteAsync(command, settings);
        exitCode.ShouldBe(1);
    }

    [Fact]
    public async Task DailyChange_WhenCostSpikes_ExceedsThreshold_FailOnThresholdFalse_Returns0()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        _mockRetriever
            .Setup(r => r.RetrieveCosts(
                It.IsAny<bool>(), It.IsAny<Scope>(), It.IsAny<string[]>(),
                It.IsAny<MetricType>(), It.IsAny<TimeframeType>(),
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<CostItem>
            {
                new(today,    200.0, 200.0, "USD"),
                new(yesterday, 100.0, 100.0, "USD"),
            });

        var command = new DailyChangeThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            Percentage = 20.0,
            FailOnThreshold = false,
            Output = OutputFormat.Text,
        };

        var exitCode = await InvokeExecuteAsync(command, settings);
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task DailyChange_EmptyCostList_DoesNotThrow_Returns0()
    {
        _mockRetriever
            .Setup(r => r.RetrieveCosts(
                It.IsAny<bool>(), It.IsAny<Scope>(), It.IsAny<string[]>(),
                It.IsAny<MetricType>(), It.IsAny<TimeframeType>(),
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<CostItem>());

        var command = new DailyChangeThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            Percentage = 20.0,
            Output = OutputFormat.Text,
        };

        var exitCode = await InvokeExecuteAsync(command, settings);
        exitCode.ShouldBe(0);
    }

    // -----------------------------------------------------------------------
    // WeeklyAverageThresholdCommand — Execute logic tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task WeeklyAverage_BelowFixedAmount_Returns0()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        _mockRetriever
            .Setup(r => r.RetrieveCosts(
                It.IsAny<bool>(), It.IsAny<Scope>(), It.IsAny<string[]>(),
                It.IsAny<MetricType>(), It.IsAny<TimeframeType>(),
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(Enumerable.Range(0, 7)
                .Select(i => new CostItem(today.AddDays(-i), 10.0, 10.0, "USD"))
                .ToList());

        var command = new WeeklyAverageThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            FixedAmount = 50.0,   // average = 10, well below 50
            FailOnThreshold = true,
            Output = OutputFormat.Text,
        };

        var exitCode = await InvokeExecuteAsync(command, settings);
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task WeeklyAverage_AboveFixedAmount_Returns1()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        _mockRetriever
            .Setup(r => r.RetrieveCosts(
                It.IsAny<bool>(), It.IsAny<Scope>(), It.IsAny<string[]>(),
                It.IsAny<MetricType>(), It.IsAny<TimeframeType>(),
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(Enumerable.Range(0, 7)
                .Select(i => new CostItem(today.AddDays(-i), 100.0, 100.0, "USD"))
                .ToList());

        var command = new WeeklyAverageThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            FixedAmount = 50.0,   // average = 100, exceeds 50
            FailOnThreshold = true,
            Output = OutputFormat.Text,
        };

        var exitCode = await InvokeExecuteAsync(command, settings);
        exitCode.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // ForecastDeviationThresholdCommand — Execute logic tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ForecastDeviation_ActualEqualsForecast_Returns0()
    {
        var from = new DateOnly(2024, 1, 1);
        var to = new DateOnly(2024, 1, 31);

        _mockRetriever
            .Setup(r => r.RetrieveCosts(It.IsAny<bool>(), It.IsAny<Scope>(), It.IsAny<string[]>(),
                It.IsAny<MetricType>(), It.IsAny<TimeframeType>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<CostItem> { new(from, 500.0, 500.0, "EUR") });

        _mockRetriever
            .Setup(r => r.RetrieveForecastedCosts(It.IsAny<bool>(), It.IsAny<Scope>(), It.IsAny<string[]>(),
                It.IsAny<MetricType>(), It.IsAny<TimeframeType>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<CostItem> { new(from, 500.0, 500.0, "EUR") });

        var command = new ForecastDeviationThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            Percentage = 10.0,
            FailOnThreshold = true,
            Output = OutputFormat.Text,
            From = from,
            To = to,
        };

        var exitCode = await InvokeExecuteAsync(command, settings);
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task ForecastDeviation_ActualFarExceedsForecast_Returns1()
    {
        var from = new DateOnly(2024, 1, 1);
        var to = new DateOnly(2024, 1, 31);

        _mockRetriever
            .Setup(r => r.RetrieveCosts(It.IsAny<bool>(), It.IsAny<Scope>(), It.IsAny<string[]>(),
                It.IsAny<MetricType>(), It.IsAny<TimeframeType>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<CostItem> { new(from, 1000.0, 1000.0, "EUR") });

        _mockRetriever
            .Setup(r => r.RetrieveForecastedCosts(It.IsAny<bool>(), It.IsAny<Scope>(), It.IsAny<string[]>(),
                It.IsAny<MetricType>(), It.IsAny<TimeframeType>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(new List<CostItem> { new(from, 500.0, 500.0, "EUR") });

        var command = new ForecastDeviationThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            Percentage = 10.0,   // 100% deviation > 10%
            FailOnThreshold = true,
            Output = OutputFormat.Text,
            From = from,
            To = to,
        };

        var exitCode = await InvokeExecuteAsync(command, settings);
        exitCode.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // ServiceSpikeThresholdCommand — Execute logic tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ServiceSpike_NoSpike_Returns0()
    {
        var to = new DateOnly(2024, 6, 30);
        var from = new DateOnly(2024, 6, 1);

        var sameServices = new List<CostNamedItem>
        {
            new("Compute", 200.0, 200.0, "USD"),
            new("Storage", 50.0, 50.0, "USD"),
        };

        _mockRetriever
            .Setup(r => r.RetrieveCostByServiceName(It.IsAny<bool>(), It.IsAny<Scope>(), It.IsAny<string[]>(),
                It.IsAny<MetricType>(), It.IsAny<TimeframeType>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(sameServices);

        var command = new ServiceSpikeThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            Percentage = 20.0,
            FailOnThreshold = true,
            Output = OutputFormat.Text,
            From = from,
            To = to,
        };

        var exitCode = await InvokeExecuteAsync(command, settings);
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task ServiceSpike_MassiveSpike_Returns1()
    {
        var to = new DateOnly(2024, 6, 30);
        var from = new DateOnly(2024, 6, 1);

        var prevServices = new List<CostNamedItem>
        {
            new("Compute", 50.0, 50.0, "USD"),
        };
        var currServices = new List<CostNamedItem>
        {
            new("Compute", 500.0, 500.0, "USD"),  // 900% increase
        };

        _mockRetriever
            .SetupSequence(r => r.RetrieveCostByServiceName(It.IsAny<bool>(), It.IsAny<Scope>(), It.IsAny<string[]>(),
                It.IsAny<MetricType>(), It.IsAny<TimeframeType>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
            .ReturnsAsync(currServices)
            .ReturnsAsync(prevServices);

        var command = new ServiceSpikeThresholdCommand(_mockRetriever.Object);
        var settings = new ThresholdSettings
        {
            Subscription = Guid.NewGuid(),
            Percentage = 20.0,
            FailOnThreshold = true,
            Output = OutputFormat.Text,
            From = from,
            To = to,
        };

        var exitCode = await InvokeExecuteAsync(command, settings);
        exitCode.ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static CommandContext CreateCommandContext()
    {
        var remaining = Mock.Of<IRemainingArguments>();
        return new CommandContext([], remaining, "threshold", null);
    }

    /// <summary>
    /// Invokes ExecuteAsync via reflection (same pattern as existing tests in this project).
    /// </summary>
    private static async Task<int> InvokeExecuteAsync<TSettings>(AsyncCommand<TSettings> command, TSettings settings)
        where TSettings : CommandSettings
    {
        var ctx = CreateCommandContext();
        var method = typeof(AsyncCommand<TSettings>)
            .GetMethod("ExecuteAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(CommandContext), typeof(TSettings), typeof(CancellationToken) },
                null);

        if (method == null)
        {
            // fallback: two-parameter overload (without CancellationToken)
            method = typeof(AsyncCommand<TSettings>)
                .GetMethod("ExecuteAsync",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }

        var task = (Task<int>)method!.Invoke(command, new object[] { ctx, settings, CancellationToken.None })!;
        return await task;
    }
}
