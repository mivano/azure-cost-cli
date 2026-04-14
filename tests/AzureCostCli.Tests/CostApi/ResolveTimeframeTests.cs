using AzureCostCli.Commands;
using AzureCostCli.CostApi;
using Shouldly;
using Xunit;

namespace AzureCostCli.Tests.CostApi;

public class ResolveTimeframeTests
{
    [Fact]
    public void TheLastMonth_ReturnsCustom_WithPreviousMonthDates()
    {
        var today = new DateOnly(2026, 4, 14);
        var dummyFrom = new DateOnly(2026, 1, 1);
        var dummyTo = new DateOnly(2026, 1, 31);

        var (timeFrame, from, to) = AzureCostApiRetriever.ResolveTimeframe(
            TimeframeType.TheLastMonth, dummyFrom, dummyTo, today);

        timeFrame.ShouldBe(TimeframeType.Custom);
        from.ShouldBe(new DateOnly(2026, 3, 1));
        to.ShouldBe(new DateOnly(2026, 3, 31));
    }

    [Fact]
    public void TheLastBillingMonth_ReturnsCustom_WithPreviousMonthDates()
    {
        var today = new DateOnly(2026, 4, 14);
        var dummyFrom = new DateOnly(2026, 1, 1);
        var dummyTo = new DateOnly(2026, 1, 31);

        var (timeFrame, from, to) = AzureCostApiRetriever.ResolveTimeframe(
            TimeframeType.TheLastBillingMonth, dummyFrom, dummyTo, today);

        timeFrame.ShouldBe(TimeframeType.Custom);
        from.ShouldBe(new DateOnly(2026, 3, 1));
        to.ShouldBe(new DateOnly(2026, 3, 31));
    }

    [Fact]
    public void TheLastMonth_OnFirstDayOfMonth_ReturnsPreviousMonth()
    {
        var today = new DateOnly(2026, 3, 1);

        var (timeFrame, from, to) = AzureCostApiRetriever.ResolveTimeframe(
            TimeframeType.TheLastMonth, default, default, today);

        timeFrame.ShouldBe(TimeframeType.Custom);
        from.ShouldBe(new DateOnly(2026, 2, 1));
        to.ShouldBe(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public void TheLastMonth_InJanuary_ReturnsDecemberOfPreviousYear()
    {
        var today = new DateOnly(2026, 1, 15);

        var (timeFrame, from, to) = AzureCostApiRetriever.ResolveTimeframe(
            TimeframeType.TheLastMonth, default, default, today);

        timeFrame.ShouldBe(TimeframeType.Custom);
        from.ShouldBe(new DateOnly(2025, 12, 1));
        to.ShouldBe(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void TheLastMonth_InMarch_HandlesFebruaryLeapYear()
    {
        var today = new DateOnly(2024, 3, 10); // 2024 is a leap year

        var (timeFrame, from, to) = AzureCostApiRetriever.ResolveTimeframe(
            TimeframeType.TheLastMonth, default, default, today);

        timeFrame.ShouldBe(TimeframeType.Custom);
        from.ShouldBe(new DateOnly(2024, 2, 1));
        to.ShouldBe(new DateOnly(2024, 2, 29));
    }

    [Fact]
    public void Custom_PassesThroughUnchanged()
    {
        var from = new DateOnly(2026, 2, 1);
        var to = new DateOnly(2026, 2, 28);

        var (timeFrame, resolvedFrom, resolvedTo) = AzureCostApiRetriever.ResolveTimeframe(
            TimeframeType.Custom, from, to, new DateOnly(2026, 4, 14));

        timeFrame.ShouldBe(TimeframeType.Custom);
        resolvedFrom.ShouldBe(from);
        resolvedTo.ShouldBe(to);
    }

    [Theory]
    [InlineData(TimeframeType.MonthToDate)]
    [InlineData(TimeframeType.BillingMonthToDate)]
    [InlineData(TimeframeType.WeekToDate)]
    public void OtherTimeframes_PassThroughUnchanged(TimeframeType inputTimeframe)
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);

        var (timeFrame, resolvedFrom, resolvedTo) = AzureCostApiRetriever.ResolveTimeframe(
            inputTimeframe, from, to, new DateOnly(2026, 4, 14));

        timeFrame.ShouldBe(inputTimeframe);
        resolvedFrom.ShouldBe(from);
        resolvedTo.ShouldBe(to);
    }
}
