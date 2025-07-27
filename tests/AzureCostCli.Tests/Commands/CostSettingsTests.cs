using AzureCostCli.Commands;
using Shouldly;
using Xunit;

namespace AzureCostCli.Tests.Commands;

public class CostSettingsTests
{
    [Fact]
    public void GetScope_WithSubscriptionAndResourceGroup_ReturnsResourceGroupScope()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var resourceGroup = "test-rg";
        var settings = new CostSettings
        {
            Subscription = subscriptionId,
            ResourceGroup = resourceGroup
        };

        // Act
        var scope = settings.GetScope;

        // Assert
        scope.Name.ShouldBe("ResourceGroup");
        scope.ScopePath.ShouldBe($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}");
        scope.IsSubscriptionBased.ShouldBeTrue();
    }

    [Fact]
    public void GetScope_WithEnrollmentAndBillingAccount_ReturnsEnrollmentAccountScope()
    {
        // Arrange
        var billingAccountId = "12345";
        var enrollmentAccountId = "67890";
        var settings = new CostSettings
        {
            BillingAccountId = billingAccountId,
            EnrollmentAccountId = enrollmentAccountId,
            Subscription = null
        };

        // Act
        var scope = settings.GetScope;

        // Assert
        scope.Name.ShouldBe("EnrollmentAccount");
        scope.ScopePath.ShouldBe($"/providers/Microsoft.Billing/billingAccounts/{billingAccountId}/enrollmentAccounts/{enrollmentAccountId}");
        scope.IsSubscriptionBased.ShouldBeFalse();
    }

    [Fact]
    public void GetScope_WithBillingAccountOnly_ReturnsBillingAccountScope()
    {
        // Arrange
        var billingAccountId = "12345";
        var settings = new CostSettings
        {
            BillingAccountId = billingAccountId,
            Subscription = null
        };

        // Act
        var scope = settings.GetScope;

        // Assert
        scope.Name.ShouldBe("BillingAccount");
        scope.ScopePath.ShouldBe($"/providers/Microsoft.Billing/billingAccounts/{billingAccountId}");
        scope.IsSubscriptionBased.ShouldBeFalse();
    }

    [Fact]
    public void GetScope_WithSubscriptionOnly_ReturnsSubscriptionScope()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var settings = new CostSettings
        {
            Subscription = subscriptionId
        };

        // Act
        var scope = settings.GetScope;

        // Assert
        scope.Name.ShouldBe("Subscription");
        scope.ScopePath.ShouldBe($"/subscriptions/{subscriptionId}");
        scope.IsSubscriptionBased.ShouldBeTrue();
    }

    [Fact]
    public void GetScope_WithEmptyGuidSubscription_ReturnsSubscriptionScopeWithEmptyGuid()
    {
        // Arrange
        var settings = new CostSettings
        {
            Subscription = Guid.Empty
        };

        // Act
        var scope = settings.GetScope;

        // Assert
        scope.Name.ShouldBe("Subscription");
        scope.ScopePath.ShouldBe($"/subscriptions/{Guid.Empty}");
        scope.IsSubscriptionBased.ShouldBeTrue();
    }

    [Fact]
    public void GetScope_WithNoSettings_ReturnsSubscriptionScopeWithEmptyGuid()
    {
        // Arrange
        var settings = new CostSettings();

        // Act
        var scope = settings.GetScope;

        // Assert
        scope.Name.ShouldBe("Subscription");
        scope.ScopePath.ShouldBe($"/subscriptions/{Guid.Empty}");
        scope.IsSubscriptionBased.ShouldBeTrue();
    }

    [Fact]
    public void CostSettings_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var settings = new CostSettings();

        // Assert
        settings.Output.ShouldBe(OutputFormat.Console);
        settings.Timeframe.ShouldBe(TimeframeType.BillingMonthToDate);
        settings.OthersCutoff.ShouldBe(10);
        settings.Query.ShouldBe(string.Empty);
        settings.UseUSD.ShouldBeFalse();
        settings.SkipHeader.ShouldBeFalse();
        settings.Filter.ShouldBeEmpty();
        settings.Metric.ShouldBe(MetricType.ActualCost);
        settings.IncludeTags.ShouldBeFalse();
        settings.CostApiAddress.ShouldBe("https://management.azure.com/");
        settings.PriceApiAddress.ShouldBe("https://prices.azure.com/");
        settings.HttpTimeout.ShouldBe(100);
    }
}

public class ScopeTests
{
    [Fact]
    public void Subscription_CreatesCorrectScope()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();

        // Act
        var scope = Scope.Subscription(subscriptionId);

        // Assert
        scope.Name.ShouldBe("Subscription");
        scope.ScopePath.ShouldBe($"/subscriptions/{subscriptionId}");
        scope.IsSubscriptionBased.ShouldBeTrue();
    }

    [Fact]
    public void ResourceGroup_CreatesCorrectScope()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var resourceGroup = "test-rg";

        // Act
        var scope = Scope.ResourceGroup(subscriptionId, resourceGroup);

        // Assert
        scope.Name.ShouldBe("ResourceGroup");
        scope.ScopePath.ShouldBe($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}");
        scope.IsSubscriptionBased.ShouldBeTrue();
    }

    [Fact]
    public void EnrollmentAccount_CreatesCorrectScope()
    {
        // Arrange
        var billingAccountId = "12345";
        var enrollmentAccountId = "67890";

        // Act
        var scope = Scope.EnrollmentAccount(billingAccountId, enrollmentAccountId);

        // Assert
        scope.Name.ShouldBe("EnrollmentAccount");
        scope.ScopePath.ShouldBe($"/providers/Microsoft.Billing/billingAccounts/{billingAccountId}/enrollmentAccounts/{enrollmentAccountId}");
        scope.IsSubscriptionBased.ShouldBeFalse();
    }

    [Fact]
    public void BillingAccount_CreatesCorrectScope()
    {
        // Arrange
        var billingAccountId = "12345";

        // Act
        var scope = Scope.BillingAccount(billingAccountId);

        // Assert
        scope.Name.ShouldBe("BillingAccount");
        scope.ScopePath.ShouldBe($"/providers/Microsoft.Billing/billingAccounts/{billingAccountId}");
        scope.IsSubscriptionBased.ShouldBeFalse();
    }
}