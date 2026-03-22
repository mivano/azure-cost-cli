using AzureCostCli.Commands.DetectAnomaly;
using AzureCostCli.CostApi;
using Shouldly;
using Xunit;

namespace AzureCostCli.Tests.Commands;

public class CostAnalyzerTests
{
    private static DetectAnomalySettings CreateDefaultSettings(
        int recentActivityDays = 7,
        double significantChange = 0.5,
        int steadyGrowthDays = 7,
        double thresholdCost = 2.0,
        bool excludeRemovedCosts = false)
    {
        return new DetectAnomalySettings
        {
            RecentActivityDays = recentActivityDays,
            SignificantChange = significantChange,
            SteadyGrowthDays = steadyGrowthDays,
            ThresholdCost = thresholdCost,
            ExcludeRemovedCosts = excludeRemovedCosts
        };
    }

    private static CostDailyItem Item(string name, DateOnly date, double cost)
        => new(date, name, cost, cost, "USD", null);

    [Fact]
    public void AnalyzeCost_EmptyList_ReturnsNoAnomalies()
    {
        var analyzer = new CostAnalyzer(CreateDefaultSettings());
        var result = analyzer.AnalyzeCost(new List<CostDailyItem>());
        result.ShouldBeEmpty();
    }

    [Fact]
    public void AnalyzeCost_NewCost_DetectsNewCostAnomaly()
    {
        // Arrange: first entry has 0 cost, then a non-zero cost appears
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = new List<CostDailyItem>
        {
            Item("ResourceA", today.AddDays(-10), 0.0),
            Item("ResourceA", today.AddDays(-9), 0.0),
            Item("ResourceA", today.AddDays(-8), 50.0),
            Item("ResourceA", today.AddDays(-1), 55.0),
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings());
        var results = analyzer.AnalyzeCost(items);

        results.ShouldContain(r => r.AnomalyType == AnomalyType.NewCost && r.Name == "ResourceA");
    }

    [Fact]
    public void AnalyzeCost_NewCost_BelowThreshold_NotDetected()
    {
        // New cost that is below the threshold should not be detected
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = new List<CostDailyItem>
        {
            Item("ResourceA", today.AddDays(-5), 0.0),
            Item("ResourceA", today.AddDays(-1), 1.0), // Below threshold of 2.0
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings(thresholdCost: 2.0));
        var results = analyzer.AnalyzeCost(items);

        results.ShouldNotContain(r => r.AnomalyType == AnomalyType.NewCost);
    }

    [Fact]
    public void AnalyzeCost_RemovedCost_DetectsRemovedCostAnomaly()
    {
        // Arrange: resource had costs but last entry is 0
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = new List<CostDailyItem>
        {
            Item("ResourceB", today.AddDays(-5), 100.0),
            Item("ResourceB", today.AddDays(-4), 95.0),
            Item("ResourceB", today.AddDays(-3), 98.0),
            Item("ResourceB", today.AddDays(-2), 0.0),
            Item("ResourceB", today.AddDays(-1), 0.0),
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings());
        var results = analyzer.AnalyzeCost(items);

        results.ShouldContain(r => r.AnomalyType == AnomalyType.RemovedCost && r.Name == "ResourceB");
    }

    [Fact]
    public void AnalyzeCost_RemovedCost_WhenExcluded_NotDetected()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = new List<CostDailyItem>
        {
            Item("ResourceB", today.AddDays(-5), 100.0),
            Item("ResourceB", today.AddDays(-1), 0.0),
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings(excludeRemovedCosts: true));
        var results = analyzer.AnalyzeCost(items);

        results.ShouldNotContain(r => r.AnomalyType == AnomalyType.RemovedCost);
    }

    [Fact]
    public void AnalyzeCost_SignificantCostChange_DetectedWhenAboveThreshold()
    {
        // Resource cost doubles - should be a significant change
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = new List<CostDailyItem>
        {
            Item("ResourceC", today.AddDays(-3), 50.0),
            Item("ResourceC", today.AddDays(-2), 52.0),
            Item("ResourceC", today.AddDays(-1), 130.0), // >50% jump from 52
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings(significantChange: 0.5, thresholdCost: 2.0));
        var results = analyzer.AnalyzeCost(items);

        results.ShouldContain(r => r.AnomalyType == AnomalyType.SignificantChange && r.Name == "ResourceC");
    }

    [Fact]
    public void AnalyzeCost_SignificantCostChange_NotDetectedWhenSmallChange()
    {
        // Small cost increase - not significant
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = new List<CostDailyItem>
        {
            Item("ResourceD", today.AddDays(-3), 100.0),
            Item("ResourceD", today.AddDays(-2), 105.0), // 5% increase only
            Item("ResourceD", today.AddDays(-1), 110.0),
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings(significantChange: 0.5));
        var results = analyzer.AnalyzeCost(items);

        results.ShouldNotContain(r => r.AnomalyType == AnomalyType.SignificantChange && r.Name == "ResourceD");
    }

    [Fact]
    public void AnalyzeCost_SteadyGrowth_DetectedOverConfiguredDays()
    {
        // Resource with steadily increasing costs over 7 days
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = new List<CostDailyItem>
        {
            Item("ResourceE", today.AddDays(-7), 10.0),
            Item("ResourceE", today.AddDays(-6), 11.0),
            Item("ResourceE", today.AddDays(-5), 12.0),
            Item("ResourceE", today.AddDays(-4), 13.0),
            Item("ResourceE", today.AddDays(-3), 14.0),
            Item("ResourceE", today.AddDays(-2), 15.0),
            Item("ResourceE", today.AddDays(-1), 16.0),
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings(steadyGrowthDays: 7));
        var results = analyzer.AnalyzeCost(items);

        results.ShouldContain(r => r.AnomalyType == AnomalyType.SteadyGrowth && r.Name == "ResourceE");
    }

    [Fact]
    public void AnalyzeCost_SteadyGrowth_NotDetectedWhenGrowthDips()
    {
        // Cost goes up then down - not steady growth
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = new List<CostDailyItem>
        {
            Item("ResourceF", today.AddDays(-7), 10.0),
            Item("ResourceF", today.AddDays(-6), 11.0),
            Item("ResourceF", today.AddDays(-5), 12.0),
            Item("ResourceF", today.AddDays(-4), 11.5), // dip
            Item("ResourceF", today.AddDays(-3), 13.0),
            Item("ResourceF", today.AddDays(-2), 14.0),
            Item("ResourceF", today.AddDays(-1), 15.0),
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings(steadyGrowthDays: 7));
        var results = analyzer.AnalyzeCost(items);

        results.ShouldNotContain(r => r.AnomalyType == AnomalyType.SteadyGrowth && r.Name == "ResourceF");
    }

    [Fact]
    public void AnalyzeCost_SteadyGrowth_NotDetectedWhenInsufficientData()
    {
        // Not enough data points for steady growth detection
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = new List<CostDailyItem>
        {
            Item("ResourceG", today.AddDays(-3), 10.0),
            Item("ResourceG", today.AddDays(-2), 11.0),
            Item("ResourceG", today.AddDays(-1), 12.0),
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings(steadyGrowthDays: 7));
        var results = analyzer.AnalyzeCost(items);

        results.ShouldNotContain(r => r.AnomalyType == AnomalyType.SteadyGrowth && r.Name == "ResourceG");
    }

    [Fact]
    public void AnalyzeCost_InactiveResource_ExcludedFromSignificantChangeAndSteadyGrowthDetection()
    {
        // Resource with old data only (beyond recent activity window) should not trigger
        // significant change or steady growth anomalies
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = new List<CostDailyItem>
        {
            Item("OldResource", today.AddDays(-100), 50.0),
            Item("OldResource", today.AddDays(-99), 200.0), // Big jump, but old
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings(recentActivityDays: 7));
        var results = analyzer.AnalyzeCost(items);

        results.ShouldNotContain(r =>
            (r.AnomalyType == AnomalyType.SignificantChange || r.AnomalyType == AnomalyType.SteadyGrowth)
            && r.Name == "OldResource");
    }

    [Fact]
    public void AnalyzeCost_MultipleResources_DetectsAnomaliesForEach()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = new List<CostDailyItem>
        {
            // Resource with new cost
            Item("NewRes", today.AddDays(-5), 0.0),
            Item("NewRes", today.AddDays(-1), 50.0),

            // Resource with steady growth
            Item("GrowRes", today.AddDays(-7), 10.0),
            Item("GrowRes", today.AddDays(-6), 11.0),
            Item("GrowRes", today.AddDays(-5), 12.0),
            Item("GrowRes", today.AddDays(-4), 13.0),
            Item("GrowRes", today.AddDays(-3), 14.0),
            Item("GrowRes", today.AddDays(-2), 15.0),
            Item("GrowRes", today.AddDays(-1), 16.0),
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings());
        var results = analyzer.AnalyzeCost(items);

        results.ShouldContain(r => r.AnomalyType == AnomalyType.NewCost && r.Name == "NewRes");
        results.ShouldContain(r => r.AnomalyType == AnomalyType.SteadyGrowth && r.Name == "GrowRes");
    }

    [Fact]
    public void AnalyzeCost_RemovedCostMessage_ContainsCorrectDate()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var lastCostDate = today.AddDays(-3);
        var items = new List<CostDailyItem>
        {
            Item("ResourceH", today.AddDays(-5), 100.0),
            Item("ResourceH", lastCostDate, 100.0),
            Item("ResourceH", today.AddDays(-2), 0.0),
            Item("ResourceH", today.AddDays(-1), 0.0),
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings());
        var results = analyzer.AnalyzeCost(items);

        var removedResult = results.FirstOrDefault(r => r.AnomalyType == AnomalyType.RemovedCost);
        removedResult.ShouldNotBeNull();
        removedResult!.CostDifference.ShouldBe(-100.0);
        removedResult.DetectionDate.ShouldBe(lastCostDate.AddDays(1));
    }

    [Fact]
    public void AnalyzeCost_SignificantChange_CostDifferenceIsPositiveForIncrease()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var items = new List<CostDailyItem>
        {
            Item("ResourceI", today.AddDays(-2), 50.0),
            Item("ResourceI", today.AddDays(-1), 200.0), // 300% increase
        };

        var analyzer = new CostAnalyzer(CreateDefaultSettings(significantChange: 0.5, thresholdCost: 2.0));
        var results = analyzer.AnalyzeCost(items);

        var anomaly = results.FirstOrDefault(r => r.AnomalyType == AnomalyType.SignificantChange && r.Name == "ResourceI");
        anomaly.ShouldNotBeNull();
        anomaly!.CostDifference.ShouldBe(150.0);
    }
}
