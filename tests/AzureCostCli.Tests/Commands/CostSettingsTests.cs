using AzureCostCli.Commands;
using FluentAssertions;
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
        scope.Name.Should().Be("ResourceGroup");
        scope.ScopePath.Should().Be($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}");
        scope.IsSubscriptionBased.Should().BeTrue();
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
        scope.Name.Should().Be("EnrollmentAccount");
        scope.ScopePath.Should().Be($"/providers/Microsoft.Billing/billingAccounts/{billingAccountId}/enrollmentAccounts/{enrollmentAccountId}");
        scope.IsSubscriptionBased.Should().BeFalse();
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
        scope.Name.Should().Be("BillingAccount");
        scope.ScopePath.Should().Be($"/providers/Microsoft.Billing/billingAccounts/{billingAccountId}");
        scope.IsSubscriptionBased.Should().BeFalse();
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
        scope.Name.Should().Be("Subscription");
        scope.ScopePath.Should().Be($"/subscriptions/{subscriptionId}");
        scope.IsSubscriptionBased.Should().BeTrue();
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
        scope.Name.Should().Be("Subscription");
        scope.ScopePath.Should().Be($"/subscriptions/{Guid.Empty}");
        scope.IsSubscriptionBased.Should().BeTrue();
    }

    [Fact]
    public void GetScope_WithNoSettings_ReturnsSubscriptionScopeWithEmptyGuid()
    {
        // Arrange
        var settings = new CostSettings();

        // Act
        var scope = settings.GetScope;

        // Assert
        scope.Name.Should().Be("Subscription");
        scope.ScopePath.Should().Be($"/subscriptions/{Guid.Empty}");
        scope.IsSubscriptionBased.Should().BeTrue();
    }

    [Fact]
    public void CostSettings_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var settings = new CostSettings();

        // Assert
        settings.Output.Should().Be(OutputFormat.Console);
        settings.Timeframe.Should().Be(TimeframeType.BillingMonthToDate);
        settings.OthersCutoff.Should().Be(10);
        settings.Query.Should().Be(string.Empty);
        settings.UseUSD.Should().BeFalse();
        settings.SkipHeader.Should().BeFalse();
        settings.Filter.Should().BeEmpty();
        settings.Metric.Should().Be(MetricType.ActualCost);
        settings.IncludeTags.Should().BeFalse();
        settings.CostApiAddress.Should().Be("https://management.azure.com/");
        settings.PriceApiAddress.Should().Be("https://prices.azure.com/");
        settings.HttpTimeout.Should().Be(100);
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
        scope.Name.Should().Be("Subscription");
        scope.ScopePath.Should().Be($"/subscriptions/{subscriptionId}");
        scope.IsSubscriptionBased.Should().BeTrue();
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
        scope.Name.Should().Be("ResourceGroup");
        scope.ScopePath.Should().Be($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}");
        scope.IsSubscriptionBased.Should().BeTrue();
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
        scope.Name.Should().Be("EnrollmentAccount");
        scope.ScopePath.Should().Be($"/providers/Microsoft.Billing/billingAccounts/{billingAccountId}/enrollmentAccounts/{enrollmentAccountId}");
        scope.IsSubscriptionBased.Should().BeFalse();
    }

    [Fact]
    public void BillingAccount_CreatesCorrectScope()
    {
        // Arrange
        var billingAccountId = "12345";

        // Act
        var scope = Scope.BillingAccount(billingAccountId);

        // Assert
        scope.Name.Should().Be("BillingAccount");
        scope.ScopePath.Should().Be($"/providers/Microsoft.Billing/billingAccounts/{billingAccountId}");
        scope.IsSubscriptionBased.Should().BeFalse();
    }
}