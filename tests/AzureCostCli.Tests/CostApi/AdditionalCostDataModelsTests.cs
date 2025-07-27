using AzureCostCli.CostApi;
using FluentAssertions;
using Xunit;

namespace AzureCostCli.Tests.CostApi;

public class AdditionalCostDataModelsTests
{
    [Fact]
    public void Subscription_CanBeCreated_WithAllProperties()
    {
        // Arrange
        var id = "/subscriptions/12345";
        var authorizationSource = "RoleBased";
        var managedByTenants = new object[] { };
        var subscriptionId = "12345";
        var tenantId = "67890";
        var displayName = "Test Subscription";
        var state = "Enabled";
        var subscriptionPolicies = new SubscriptionPolicies("East US", "PayAsYouGo_2014-09-01", "On");

        // Act
        var subscription = new Subscription(
            id, authorizationSource, managedByTenants, subscriptionId, 
            tenantId, displayName, state, subscriptionPolicies);

        // Assert
        subscription.id.Should().Be(id);
        subscription.authorizationSource.Should().Be(authorizationSource);
        subscription.managedByTenants.Should().BeEquivalentTo(managedByTenants);
        subscription.subscriptionId.Should().Be(subscriptionId);
        subscription.tenantId.Should().Be(tenantId);
        subscription.displayName.Should().Be(displayName);
        subscription.state.Should().Be(state);
        subscription.subscriptionPolicies.Should().Be(subscriptionPolicies);
    }

    [Fact]
    public void SubscriptionPolicies_CanBeCreated_WithAllProperties()
    {
        // Arrange
        var locationPlacementId = "East US";
        var quotaId = "PayAsYouGo_2014-09-01";
        var spendingLimit = "On";

        // Act
        var policies = new SubscriptionPolicies(locationPlacementId, quotaId, spendingLimit);

        // Assert
        policies.locationPlacementId.Should().Be(locationPlacementId);
        policies.quotaId.Should().Be(quotaId);
        policies.spendingLimit.Should().Be(spendingLimit);
    }

    [Fact]
    public void EnrollmentAccount_CanBeCreated_WithAllProperties()
    {
        // Arrange
        var id = "/providers/Microsoft.Billing/billingAccounts/123456/enrollmentAccounts/234567";
        var name = "234567";
        var props = new properties("Test Account", "IT", "Test Display Name");

        // Act
        var enrollmentAccount = new EnrollmentAccount(id, name, props);

        // Assert
        enrollmentAccount.id.Should().Be(id);
        enrollmentAccount.name.Should().Be(name);
        enrollmentAccount.properties.Should().Be(props);
    }

    [Fact]
    public void Properties_CanBeCreated_WithAllProperties()
    {
        // Arrange
        var accountName = "Test Account";
        var costCenter = "IT";
        var displayName = "Test Display Name";

        // Act
        var properties = new properties(accountName, costCenter, displayName);

        // Assert
        properties.accountName.Should().Be(accountName);
        properties.costCenter.Should().Be(costCenter);
        properties.displayName.Should().Be(displayName);
    }

    [Fact]
    public void AzureRegion_CanBeCreated_WithAllProperties()
    {
        // Arrange & Act
        var region = new AzureRegion
        {
            id = "eastus",
            displayName = "East US",
            location = "eastus",
            continent = "NA",
            geographyId = "US",
            latitude = 37.3719,
            longitude = -79.8164,
            typeId = "Region",
            isOpen = true,
            yearOpen = 2014,
            complianceIds = new[] { "SOC", "HIPAA" },
            hasGroundStation = false,
            dataResidency = "US",
            availableTo = "Public",
            availabilityZonesId = "AZ123",
            availabilityZonesNearestRegionIds = new[] { "westus", "centralus" },
            productsByRegionLink = "https://azure.microsoft.com/en-us/global-infrastructure/services/",
            productsByRegionLinkNonRegional = "https://azure.microsoft.com/en-us/services/",
            sustainabilityIds = new[] { "Green" },
            disasterRecoveryCrossregionIds = new[] { "westus" },
            disasterRecoveryInregionIds = new[] { "eastus2" }
        };

        // Assert
        region.id.Should().Be("eastus");
        region.displayName.Should().Be("East US");
        region.location.Should().Be("eastus");
        region.continent.Should().Be("NA");
        region.geographyId.Should().Be("US");
        region.latitude.Should().Be(37.3719);
        region.longitude.Should().Be(-79.8164);
        region.typeId.Should().Be("Region");
        region.isOpen.Should().BeTrue();
        region.yearOpen.Should().Be(2014);
        region.complianceIds.Should().BeEquivalentTo(new[] { "SOC", "HIPAA" });
        region.hasGroundStation.Should().BeFalse();
        region.dataResidency.Should().Be("US");
        region.availableTo.Should().Be("Public");
        region.availabilityZonesId.Should().Be("AZ123");
        region.availabilityZonesNearestRegionIds.Should().BeEquivalentTo(new[] { "westus", "centralus" });
        region.productsByRegionLink.Should().Be("https://azure.microsoft.com/en-us/global-infrastructure/services/");
        region.productsByRegionLinkNonRegional.Should().Be("https://azure.microsoft.com/en-us/services/");
        region.sustainabilityIds.Should().BeEquivalentTo(new[] { "Green" });
        region.disasterRecoveryCrossregionIds.Should().BeEquivalentTo(new[] { "westus" });
        region.disasterRecoveryInregionIds.Should().BeEquivalentTo(new[] { "eastus2" });
    }
}