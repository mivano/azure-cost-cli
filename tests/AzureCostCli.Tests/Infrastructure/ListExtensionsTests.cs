using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using Shouldly;
using Xunit;

namespace AzureCostCli.Tests.Infrastructure;

public class ListExtensionsTests
{
    [Fact]
    public void TrimList_WithItemsLessThanThreshold_ReturnsAllItemsSortedByDescending()
    {
        // Arrange
        var items = new List<CostNamedItem>
        {
            new("Item1", 100, 100, "USD"),
            new("Item2", 200, 200, "USD"),
            new("Item3", 50, 50, "USD")
        };

        // Act
        var result = items.TrimList(10);

        // Assert
        result.Count.ShouldBe(3);
        result[0].ItemName.ShouldBe("Item2");
        result[0].Cost.ShouldBe(200);
        result[1].ItemName.ShouldBe("Item1");
        result[1].Cost.ShouldBe(100);
        result[2].ItemName.ShouldBe("Item3");
        result[2].Cost.ShouldBe(50);
    }

    [Fact]
    public void TrimList_WithItemsEqualToThreshold_ReturnsAllItemsSortedByDescending()
    {
        // Arrange
        var items = new List<CostNamedItem>
        {
            new("Item1", 100, 100, "USD"),
            new("Item2", 200, 200, "USD"),
            new("Item3", 50, 50, "USD")
        };

        // Act
        var result = items.TrimList(3);

        // Assert
        result.Count.ShouldBe(3);
        result[0].ItemName.ShouldBe("Item2");
        result[1].ItemName.ShouldBe("Item1");
        result[2].ItemName.ShouldBe("Item3");
    }

    [Fact]
    public void TrimList_WithItemsGreaterThanThreshold_ReturnsTopItemsPlusOthers()
    {
        // Arrange
        var items = new List<CostNamedItem>
        {
            new("Item1", 100, 100, "USD"),
            new("Item2", 200, 200, "USD"),
            new("Item3", 50, 50, "USD"),
            new("Item4", 75, 75, "USD"),
            new("Item5", 25, 25, "USD")
        };

        // Act
        var result = items.TrimList(3);

        // Assert
        result.Count.ShouldBe(3);
        result[0].ItemName.ShouldBe("Item2");
        result[0].Cost.ShouldBe(200);
        result[1].ItemName.ShouldBe("Item1");
        result[1].Cost.ShouldBe(100);
        result[2].ItemName.ShouldBe("Others");
        result[2].Cost.ShouldBe(150); // 50 + 75 + 25
        result[2].CostUsd.ShouldBe(150);
        result[2].Currency.ShouldBe("USD");
    }

    [Fact]
    public void TrimList_WithThresholdZero_ReturnsAllItemsSorted()
    {
        // Arrange
        var items = new List<CostNamedItem>
        {
            new("Item1", 100, 100, "USD"),
            new("Item2", 200, 200, "USD")
        };

        // Act
        var result = items.TrimList(0);

        // Assert
        result.Count.ShouldBe(2);
        result[0].ItemName.ShouldBe("Item2");
        result[1].ItemName.ShouldBe("Item1");
    }

    [Fact]
    public void TrimList_WithNegativeThreshold_ReturnsAllItemsSorted()
    {
        // Arrange
        var items = new List<CostNamedItem>
        {
            new("Item1", 100, 100, "USD"),
            new("Item2", 200, 200, "USD")
        };

        // Act
        var result = items.TrimList(-1);

        // Assert
        result.Count.ShouldBe(2);
        result[0].ItemName.ShouldBe("Item2");
        result[1].ItemName.ShouldBe("Item1");
    }

    [Fact]
    public void TrimList_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var items = new List<CostNamedItem>();

        // Act
        var result = items.TrimList(5);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void TrimList_WithDefaultThreshold_UsesThresholdOf10()
    {
        // Arrange
        var items = Enumerable.Range(1, 15)
            .Select(i => new CostNamedItem($"Item{i}", i * 10, i * 10, "USD"))
            .ToList();

        // Act
        var result = items.TrimList();

        // Assert
        result.Count.ShouldBe(10); // 9 top items + 1 "Others"
        result[0].ItemName.ShouldBe("Item15");
        result[0].Cost.ShouldBe(150);
        result[8].ItemName.ShouldBe("Item7");
        result[8].Cost.ShouldBe(70);
        result[9].ItemName.ShouldBe("Others");
        result[9].Cost.ShouldBe(210); // Sum of items 1-6: 10+20+30+40+50+60
    }
}