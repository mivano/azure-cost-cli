using AzureCostCli.CostApi;
using Shouldly;
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
        subscription.id.ShouldBe(id);
        subscription.authorizationSource.ShouldBe(authorizationSource);
        subscription.managedByTenants.ShouldBe(managedByTenants);
        subscription.subscriptionId.ShouldBe(subscriptionId);
        subscription.tenantId.ShouldBe(tenantId);
        subscription.displayName.ShouldBe(displayName);
        subscription.state.ShouldBe(state);
        subscription.subscriptionPolicies.ShouldBe(subscriptionPolicies);
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
        policies.locationPlacementId.ShouldBe(locationPlacementId);
        policies.quotaId.ShouldBe(quotaId);
        policies.spendingLimit.ShouldBe(spendingLimit);
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
        enrollmentAccount.id.ShouldBe(id);
        enrollmentAccount.name.ShouldBe(name);
        enrollmentAccount.properties.ShouldBe(props);
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
        properties.accountName.ShouldBe(accountName);
        properties.costCenter.ShouldBe(costCenter);
        properties.displayName.ShouldBe(displayName);
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
        region.id.ShouldBe("eastus");
        region.displayName.ShouldBe("East US");
        region.location.ShouldBe("eastus");
        region.continent.ShouldBe("NA");
        region.geographyId.ShouldBe("US");
        region.latitude.ShouldBe(37.3719);
        region.longitude.ShouldBe(-79.8164);
        region.typeId.ShouldBe("Region");
        region.isOpen.ShouldBeTrue();
        region.yearOpen.ShouldBe(2014);
        region.complianceIds.ShouldBe(new[] { "SOC", "HIPAA" });
        region.hasGroundStation.ShouldBeFalse();
        region.dataResidency.ShouldBe("US");
        region.availableTo.ShouldBe("Public");
        region.availabilityZonesId.ShouldBe("AZ123");
        region.availabilityZonesNearestRegionIds.ShouldBe(new[] { "westus", "centralus" });
        region.productsByRegionLink.ShouldBe("https://azure.microsoft.com/en-us/global-infrastructure/services/");
        region.productsByRegionLinkNonRegional.ShouldBe("https://azure.microsoft.com/en-us/services/");
        region.sustainabilityIds.ShouldBe(new[] { "Green" });
        region.disasterRecoveryCrossregionIds.ShouldBe(new[] { "westus" });
        region.disasterRecoveryInregionIds.ShouldBe(new[] { "eastus2" });
    }
}