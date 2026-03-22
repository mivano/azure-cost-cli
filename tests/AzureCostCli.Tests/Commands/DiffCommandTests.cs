using AzureCostCli.Commands;
using AzureCostCli.Commands.Diff;
using Moq;
using Shouldly;
using Spectre.Console.Cli;
using Xunit;

namespace AzureCostCli.Tests.Commands;

public class DiffCommandTests
{
    private readonly DiffCommand _command;

    public DiffCommandTests()
    {
        _command = new DiffCommand();
    }

    private static CommandContext CreateCommandContext()
    {
        var remainingArguments = Mock.Of<IRemainingArguments>();
        return new CommandContext([], remainingArguments, "diff", null);
    }

    [Fact]
    public void Validate_WithMissingCompareTo_ReturnsError()
    {
        var settings = new DiffSettings { CompareTo = null };
        var context = CreateCommandContext();

        var result = _command.Validate(context, settings);

        result.Successful.ShouldBeFalse();
        result.Message.ShouldContain("compare to file does not exist");
    }

    [Fact]
    public void Validate_WithEmptyCompareTo_ReturnsError()
    {
        var settings = new DiffSettings { CompareTo = "" };
        var context = CreateCommandContext();

        var result = _command.Validate(context, settings);

        result.Successful.ShouldBeFalse();
        result.Message.ShouldContain("compare to file does not exist");
    }

    [Fact]
    public void Validate_WithNonJsonCompareTo_ReturnsError()
    {
        var settings = new DiffSettings { CompareTo = "somefile.txt" };
        var context = CreateCommandContext();

        var result = _command.Validate(context, settings);

        result.Successful.ShouldBeFalse();
        result.Message.ShouldContain("compare to file does not exist");
    }

    [Fact]
    public void Validate_WithNonExistentCompareTo_ReturnsError()
    {
        var settings = new DiffSettings { CompareTo = "/nonexistent/path/file.json" };
        var context = CreateCommandContext();

        var result = _command.Validate(context, settings);

        result.Successful.ShouldBeFalse();
        result.Message.ShouldContain("compare to file does not exist");
    }

    [Fact]
    public void Validate_WithValidCompareToButMissingCompareFrom_ReturnsError()
    {
        // Create a temporary file to pass the CompareTo validation
        var tempFile = Path.GetTempFileName() + ".json";
        File.WriteAllText(tempFile, "{}");
        try
        {
            var settings = new DiffSettings { CompareTo = tempFile, CompareFrom = null };
            var context = CreateCommandContext();

            var result = _command.Validate(context, settings);

            result.Successful.ShouldBeFalse();
            result.Message.ShouldContain("compare from file does not exist");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Validate_WithValidCompareToButNonExistentCompareFrom_ReturnsError()
    {
        var tempFile = Path.GetTempFileName() + ".json";
        File.WriteAllText(tempFile, "{}");
        try
        {
            var settings = new DiffSettings
            {
                CompareTo = tempFile,
                CompareFrom = "/nonexistent/path/from.json"
            };
            var context = CreateCommandContext();

            var result = _command.Validate(context, settings);

            result.Successful.ShouldBeFalse();
            result.Message.ShouldContain("compare from file does not exist");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Validate_WithBothValidFiles_ReturnsSuccess()
    {
        var tempFileTo = Path.GetTempFileName() + ".json";
        var tempFileFrom = Path.GetTempFileName() + ".json";
        File.WriteAllText(tempFileTo, "{}");
        File.WriteAllText(tempFileFrom, "{}");
        try
        {
            var settings = new DiffSettings
            {
                CompareTo = tempFileTo,
                CompareFrom = tempFileFrom
            };
            var context = CreateCommandContext();

            var result = _command.Validate(context, settings);

            result.Successful.ShouldBeTrue();
        }
        finally
        {
            File.Delete(tempFileTo);
            File.Delete(tempFileFrom);
        }
    }

    [Fact]
    public void Validate_WithCustomTimeframeAndInvalidDates_ReturnsError()
    {
        var tempFileTo = Path.GetTempFileName() + ".json";
        var tempFileFrom = Path.GetTempFileName() + ".json";
        File.WriteAllText(tempFileTo, "{}");
        File.WriteAllText(tempFileFrom, "{}");
        try
        {
            var settings = new DiffSettings
            {
                CompareTo = tempFileTo,
                CompareFrom = tempFileFrom,
                Timeframe = TimeframeType.Custom,
                From = new DateOnly(2023, 1, 31),
                To = new DateOnly(2023, 1, 1) // From after To
            };
            var context = CreateCommandContext();

            var result = _command.Validate(context, settings);

            result.Successful.ShouldBeFalse();
            result.Message.ShouldBe("The from date must be before the to date.");
        }
        finally
        {
            File.Delete(tempFileTo);
            File.Delete(tempFileFrom);
        }
    }

    [Fact]
    public void Validate_WithBothFromAndTo_AutoSetsCustomTimeframe()
    {
        var tempFileTo = Path.GetTempFileName() + ".json";
        var tempFileFrom = Path.GetTempFileName() + ".json";
        File.WriteAllText(tempFileTo, "{}");
        File.WriteAllText(tempFileFrom, "{}");
        try
        {
            var settings = new DiffSettings
            {
                CompareTo = tempFileTo,
                CompareFrom = tempFileFrom,
                Timeframe = TimeframeType.BillingMonthToDate,
                From = new DateOnly(2023, 1, 1),
                To = new DateOnly(2023, 1, 31)
            };
            var context = CreateCommandContext();

            var result = _command.Validate(context, settings);

            result.Successful.ShouldBeTrue();
            settings.Timeframe.ShouldBe(TimeframeType.Custom);
        }
        finally
        {
            File.Delete(tempFileTo);
            File.Delete(tempFileFrom);
        }
    }

    [Fact]
    public void Constructor_ShouldNotThrow()
    {
        var command = new DiffCommand();
        command.ShouldNotBeNull();
    }
}
