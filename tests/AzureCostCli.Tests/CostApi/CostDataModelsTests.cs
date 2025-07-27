using AzureCostCli.CostApi;
using FluentAssertions;
using Xunit;

namespace AzureCostCli.Tests.CostApi;

public class CostItemTests
{
    [Fact]
    public void CostItem_CanBeCreated_WithAllProperties()
    {
        // Arrange
        var date = new DateOnly(2023, 1, 15);
        var cost = 100.50;
        var costUsd = 110.25;
        var currency = "EUR";

        // Act
        var costItem = new CostItem(date, cost, costUsd, currency);

        // Assert
        costItem.Date.Should().Be(date);
        costItem.Cost.Should().Be(cost);
        costItem.CostUsd.Should().Be(costUsd);
        costItem.Currency.Should().Be(currency);
    }

    [Fact]
    public void CostNamedItem_CanBeCreated_WithAllProperties()
    {
        // Arrange
        var itemName = "Test Resource";
        var cost = 75.25;
        var costUsd = 80.50;
        var currency = "USD";

        // Act
        var costNamedItem = new CostNamedItem(itemName, cost, costUsd, currency);

        // Assert
        costNamedItem.ItemName.Should().Be(itemName);
        costNamedItem.Cost.Should().Be(cost);
        costNamedItem.CostUsd.Should().Be(costUsd);
        costNamedItem.Currency.Should().Be(currency);
    }

    [Fact]
    public void CostDailyItem_CanBeCreated_WithTags()
    {
        // Arrange
        var date = new DateOnly(2023, 1, 15);
        var name = "Test Resource";
        var cost = 50.75;
        var costUsd = 52.25;
        var currency = "USD";
        var tags = new Dictionary<string, string>
        {
            ["Environment"] = "Production",
            ["Team"] = "Backend"
        };

        // Act
        var costDailyItem = new CostDailyItem(date, name, cost, costUsd, currency, tags);

        // Assert
        costDailyItem.Date.Should().Be(date);
        costDailyItem.Name.Should().Be(name);
        costDailyItem.Cost.Should().Be(cost);
        costDailyItem.CostUsd.Should().Be(costUsd);
        costDailyItem.Currency.Should().Be(currency);
        costDailyItem.Tags.Should().NotBeNull();
        costDailyItem.Tags.Should().HaveCount(2);
        costDailyItem.Tags!["Environment"].Should().Be("Production");
        costDailyItem.Tags["Team"].Should().Be("Backend");
    }

    [Fact]
    public void CostDailyItem_CanBeCreated_WithoutTags()
    {
        // Arrange
        var date = new DateOnly(2023, 1, 15);
        var name = "Test Resource";
        var cost = 50.75;
        var costUsd = 52.25;
        var currency = "USD";

        // Act
        var costDailyItem = new CostDailyItem(date, name, cost, costUsd, currency, null);

        // Assert
        costDailyItem.Date.Should().Be(date);
        costDailyItem.Name.Should().Be(name);
        costDailyItem.Cost.Should().Be(cost);
        costDailyItem.CostUsd.Should().Be(costUsd);
        costDailyItem.Currency.Should().Be(currency);
        costDailyItem.Tags.Should().BeNull();
    }

    [Fact]
    public void CostDailyItemWithoutTags_CanBeCreated()
    {
        // Arrange
        var date = new DateOnly(2023, 1, 15);
        var name = "Test Resource";
        var cost = 50.75;
        var costUsd = 52.25;
        var currency = "USD";

        // Act
        var costDailyItem = new CostDailyItemWithoutTags(date, name, cost, costUsd, currency);

        // Assert
        costDailyItem.Date.Should().Be(date);
        costDailyItem.Name.Should().Be(name);
        costDailyItem.Cost.Should().Be(cost);
        costDailyItem.CostUsd.Should().Be(costUsd);
        costDailyItem.Currency.Should().Be(currency);
    }

    [Fact]
    public void BudgetItem_CanBeCreated_WithAllProperties()
    {
        // Arrange
        var name = "Monthly Budget";
        var id = "/subscriptions/12345/budgets/monthly";
        var amount = 1000.0;
        var timeGrain = "Monthly";
        var startDate = new DateTime(2023, 1, 1);
        var endDate = new DateTime(2023, 12, 31);
        var currentSpendAmount = 250.50;
        var currentSpendCurrency = "USD";
        var forecastAmount = 800.0;
        var forecastCurrency = "USD";
        var notifications = new List<Notification>
        {
            new("Budget Alert", true, "GreaterThan", 80.0, ["admin@test.com"], ["Admin"], [])
        };

        // Act
        var budgetItem = new BudgetItem(
            name, id, amount, timeGrain, startDate, endDate,
            currentSpendAmount, currentSpendCurrency, forecastAmount, forecastCurrency, notifications);

        // Assert
        budgetItem.Name.Should().Be(name);
        budgetItem.Id.Should().Be(id);
        budgetItem.Amount.Should().Be(amount);
        budgetItem.TimeGrain.Should().Be(timeGrain);
        budgetItem.StartDate.Should().Be(startDate);
        budgetItem.EndDate.Should().Be(endDate);
        budgetItem.CurrentSpendAmount.Should().Be(currentSpendAmount);
        budgetItem.CurrentSpendCurrency.Should().Be(currentSpendCurrency);
        budgetItem.ForecastAmount.Should().Be(forecastAmount);
        budgetItem.ForecastCurrency.Should().Be(forecastCurrency);
        budgetItem.Notifications.Should().HaveCount(1);
    }

    [Fact]
    public void Notification_CanBeCreated_WithAllProperties()
    {
        // Arrange
        var name = "Test Notification";
        var enabled = true;
        var operatorValue = "GreaterThan";
        var threshold = 75.5;
        var contactEmails = new List<string> { "test@example.com", "admin@example.com" };
        var contactRoles = new List<string> { "Admin", "Owner" };
        var contactGroups = new List<string> { "IT Team" };

        // Act
        var notification = new Notification(name, enabled, operatorValue, threshold, contactEmails, contactRoles, contactGroups);

        // Assert
        notification.Name.Should().Be(name);
        notification.Enabled.Should().Be(enabled);
        notification.Operator.Should().Be(operatorValue);
        notification.Threshold.Should().Be(threshold);
        notification.ContactEmails.Should().BeEquivalentTo(contactEmails);
        notification.ContactRoles.Should().BeEquivalentTo(contactRoles);
        notification.ContactGroups.Should().BeEquivalentTo(contactGroups);
    }

    [Fact]
    public void CostResourceItem_CanBeCreated_WithAllProperties()
    {
        // Arrange
        var cost = 100.0;
        var costUSD = 105.0;
        var resourceId = "/subscriptions/12345/resourceGroups/test-rg/providers/Microsoft.Compute/virtualMachines/test-vm";
        var resourceType = "Microsoft.Compute/virtualMachines";
        var resourceLocation = "East US";
        var chargeType = "Usage";
        var resourceGroupName = "test-rg";
        var publisherType = "Microsoft";
        var serviceName = "Virtual Machines";
        var serviceTier = "Standard";
        var meter = "D2s v3";
        var tags = new Dictionary<string, string> { ["Environment"] = "Production" };
        var currency = "USD";

        // Act
        var costResourceItem = new CostResourceItem(
            cost, costUSD, resourceId, resourceType, resourceLocation, chargeType,
            resourceGroupName, publisherType, serviceName, serviceTier, meter, tags, currency);

        // Assert
        costResourceItem.Cost.Should().Be(cost);
        costResourceItem.CostUSD.Should().Be(costUSD);
        costResourceItem.ResourceId.Should().Be(resourceId);
        costResourceItem.ResourceType.Should().Be(resourceType);
        costResourceItem.ResourceLocation.Should().Be(resourceLocation);
        costResourceItem.ChargeType.Should().Be(chargeType);
        costResourceItem.ResourceGroupName.Should().Be(resourceGroupName);
        costResourceItem.PublisherType.Should().Be(publisherType);
        costResourceItem.ServiceName.Should().Be(serviceName);
        costResourceItem.ServiceTier.Should().Be(serviceTier);
        costResourceItem.Meter.Should().Be(meter);
        costResourceItem.Tags.Should().BeEquivalentTo(tags);
        costResourceItem.Currency.Should().Be(currency);
    }

    [Fact]
    public void CostResourceItem_GetResourceName_ExtractsCorrectName()
    {
        // Arrange
        var resource = new CostResourceItem(
            100.0, 105.0, 
            "/subscriptions/12345/resourceGroups/test-rg/providers/Microsoft.Compute/virtualMachines/my-test-vm", 
            "Microsoft.Compute/virtualMachines", "East US", "Usage", "test-rg", "Microsoft", 
            "Virtual Machines", "Standard", "D2s v3", new Dictionary<string, string>(), "USD");

        // Act
        var resourceName = resource.GetResourceName();

        // Assert
        resourceName.Should().Be("my-test-vm");
    }
}