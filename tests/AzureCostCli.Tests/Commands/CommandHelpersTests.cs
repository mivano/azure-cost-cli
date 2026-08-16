using AzureCostCli.Commands;
using AzureCostCli.Commands.AccumulatedCost;
using Shouldly;
using Spectre.Console;

namespace AzureCostCli.Tests.Commands;

public class CommandHelpersTests
{
    [Fact]
    public void ValidateAndResolveSubscription_WithSubscription_ReturnsSuccess()
    {
        // Arrange
        var subscription = (Guid?)Guid.NewGuid();
        Guid captured = Guid.Empty;

        // Act
        var result = CommandHelpers.ValidateAndResolveSubscription(
            subscription, isSubscriptionBased: true, id => captured = id);

        // Assert
        result.Successful.ShouldBeTrue();
    }

    [Fact]
    public void ValidateAndResolveSubscription_NotSubscriptionBased_ReturnsSuccess()
    {
        // Arrange & Act
        var result = CommandHelpers.ValidateAndResolveSubscription(
            subscription: null, isSubscriptionBased: false, _ => { });

        // Assert
        result.Successful.ShouldBeTrue();
    }

    [Fact]
    public void ValidateAndResolveSubscription_NoSubscriptionAndSubscriptionBased_AttemptsAzCliResolution()
    {
        // Arrange
        Guid captured = Guid.Empty;

        // Act - when az CLI is available it succeeds and calls setSubscription;
        // when az CLI is not available it returns an error.
        var result = CommandHelpers.ValidateAndResolveSubscription(
            subscription: null, isSubscriptionBased: true, id => captured = id);

        // Assert - either it resolved successfully (az CLI present) or returned an error
        if (result.Successful)
        {
            captured.ShouldNotBe(Guid.Empty);
        }
        else
        {
            captured.ShouldBe(Guid.Empty);
        }
    }

    [Fact]
    public void ValidateAndResolveSubscription_WhenAzCliResolutionFails_ErrorExplainsWhy()
    {
        // Act - environment dependent: az CLI may or may not be present.
        var result = CommandHelpers.ValidateAndResolveSubscription(
            subscription: null, isSubscriptionBased: true, _ => { });

        // Assert - a failure must say more than "unable to retrieve"; the underlying
        // reason is appended in parentheses so the user can actually diagnose it.
        if (!result.Successful)
        {
            result.Message.ShouldNotBeNull();
            result.Message.ShouldContain("az login");
            result.Message.ShouldMatch(@"\(.+\)");
        }
    }

    [Fact]
    public void ValidateTimeframe_CustomWithValidDates_ReturnsSuccess()
    {
        // Arrange
        var settings = new AccumulatedCostSettings
        {
            Timeframe = TimeframeType.Custom,
            From = new DateOnly(2024, 1, 1),
            To = new DateOnly(2024, 1, 31)
        };

        // Act
        var result = CommandHelpers.ValidateTimeframe(settings);

        // Assert
        result.Successful.ShouldBeTrue();
    }

    [Fact]
    public void ValidateTimeframe_CustomWithFromAfterTo_ReturnsError()
    {
        // Arrange
        var settings = new AccumulatedCostSettings
        {
            Timeframe = TimeframeType.Custom,
            From = new DateOnly(2024, 6, 1),
            To = new DateOnly(2024, 1, 1)
        };

        // Act
        var result = CommandHelpers.ValidateTimeframe(settings);

        // Assert
        result.Successful.ShouldBeFalse();
    }

    [Fact]
    public void ValidateTimeframe_NonCustomTimeframe_ReturnsSuccess()
    {
        // Arrange
        var settings = new AccumulatedCostSettings
        {
            Timeframe = TimeframeType.BillingMonthToDate
        };

        // Act
        var result = CommandHelpers.ValidateTimeframe(settings);

        // Assert
        result.Successful.ShouldBeTrue();
    }

    [Fact]
    public void PrintVersionIfDebug_WithDebugTrue_DoesNotThrow()
    {
        // Act & Assert - should not throw
        Should.NotThrow(() => CommandHelpers.PrintVersionIfDebug(true));
    }

    [Fact]
    public void PrintVersionIfDebug_WithDebugFalse_DoesNotThrow()
    {
        // Act & Assert - should not throw
        Should.NotThrow(() => CommandHelpers.PrintVersionIfDebug(false));
    }
}
