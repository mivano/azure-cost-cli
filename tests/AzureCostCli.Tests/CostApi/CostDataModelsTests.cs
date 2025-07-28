using AzureCostCli.CostApi;
using Shouldly;
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
        costItem.Date.ShouldBe(date);
        costItem.Cost.ShouldBe(cost);
        costItem.CostUsd.ShouldBe(costUsd);
        costItem.Currency.ShouldBe(currency);
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
        costNamedItem.ItemName.ShouldBe(itemName);
        costNamedItem.Cost.ShouldBe(cost);
        costNamedItem.CostUsd.ShouldBe(costUsd);
        costNamedItem.Currency.ShouldBe(currency);
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
        costDailyItem.Date.ShouldBe(date);
        costDailyItem.Name.ShouldBe(name);
        costDailyItem.Cost.ShouldBe(cost);
        costDailyItem.CostUsd.ShouldBe(costUsd);
        costDailyItem.Currency.ShouldBe(currency);
        costDailyItem.Tags.ShouldNotBeNull();
        costDailyItem.Tags.Count.ShouldBe(2);
        costDailyItem.Tags!["Environment"].ShouldBe("Production");
        costDailyItem.Tags["Team"].ShouldBe("Backend");
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
        costDailyItem.Date.ShouldBe(date);
        costDailyItem.Name.ShouldBe(name);
        costDailyItem.Cost.ShouldBe(cost);
        costDailyItem.CostUsd.ShouldBe(costUsd);
        costDailyItem.Currency.ShouldBe(currency);
        costDailyItem.Tags.ShouldBeNull();
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
        costDailyItem.Date.ShouldBe(date);
        costDailyItem.Name.ShouldBe(name);
        costDailyItem.Cost.ShouldBe(cost);
        costDailyItem.CostUsd.ShouldBe(costUsd);
        costDailyItem.Currency.ShouldBe(currency);
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
        budgetItem.Name.ShouldBe(name);
        budgetItem.Id.ShouldBe(id);
        budgetItem.Amount.ShouldBe(amount);
        budgetItem.TimeGrain.ShouldBe(timeGrain);
        budgetItem.StartDate.ShouldBe(startDate);
        budgetItem.EndDate.ShouldBe(endDate);
        budgetItem.CurrentSpendAmount.ShouldBe(currentSpendAmount);
        budgetItem.CurrentSpendCurrency.ShouldBe(currentSpendCurrency);
        budgetItem.ForecastAmount.ShouldBe(forecastAmount);
        budgetItem.ForecastCurrency.ShouldBe(forecastCurrency);
        budgetItem.Notifications.Count.ShouldBe(1);
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
        notification.Name.ShouldBe(name);
        notification.Enabled.ShouldBe(enabled);
        notification.Operator.ShouldBe(operatorValue);
        notification.Threshold.ShouldBe(threshold);
        notification.ContactEmails.ShouldBe(contactEmails);
        notification.ContactRoles.ShouldBe(contactRoles);
        notification.ContactGroups.ShouldBe(contactGroups);
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
        costResourceItem.Cost.ShouldBe(cost);
        costResourceItem.CostUSD.ShouldBe(costUSD);
        costResourceItem.ResourceId.ShouldBe(resourceId);
        costResourceItem.ResourceType.ShouldBe(resourceType);
        costResourceItem.ResourceLocation.ShouldBe(resourceLocation);
        costResourceItem.ChargeType.ShouldBe(chargeType);
        costResourceItem.ResourceGroupName.ShouldBe(resourceGroupName);
        costResourceItem.PublisherType.ShouldBe(publisherType);
        costResourceItem.ServiceName.ShouldBe(serviceName);
        costResourceItem.ServiceTier.ShouldBe(serviceTier);
        costResourceItem.Meter.ShouldBe(meter);
        costResourceItem.Tags.ShouldBe(tags);
        costResourceItem.Currency.ShouldBe(currency);
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
        resourceName.ShouldBe("my-test-vm");
    }
}