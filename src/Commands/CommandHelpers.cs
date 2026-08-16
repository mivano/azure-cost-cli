using AzureCostCli.Infrastructure;
using Spectre.Console;

namespace AzureCostCli.Commands;

/// <summary>
/// Shared validation and utility methods for commands.
/// </summary>
public static class CommandHelpers
{
    /// <summary>
    /// Resolves the subscription ID from settings or Azure CLI fallback.
    /// Returns a ValidationResult error if subscription is required but cannot be resolved.
    /// Sets the subscription on the settings if resolved from Azure CLI.
    /// </summary>
    public static ValidationResult ValidateAndResolveSubscription(Guid? subscription, bool isSubscriptionBased, Action<Guid> setSubscription)
    {
        if (!isSubscriptionBased || subscription.HasValue)
            return ValidationResult.Success();

        string reason;

        try
        {
            var subscriptionId = AzCommand.GetDefaultAzureSubscriptionId();

            if (Guid.TryParse(subscriptionId, out var resolved))
            {
                setSubscription(resolved);
                return ValidationResult.Success();
            }

            reason = $"the Azure CLI returned an unexpected subscription ID: '{subscriptionId}'";
        }
        catch (Exception ex)
        {
            reason = ex.Message;
        }

        return ValidationResult.Error(
            "No subscription ID provided and unable to retrieve from Azure CLI. " +
            "Please specify a subscription ID using -s or --subscription, " +
            "or login to Azure CLI using 'az login'. Use --help for more information. " +
            $"({reason})");
    }

    /// <summary>
    /// Validates timeframe settings (auto-apply custom timeframe, check date ordering).
    /// </summary>
    public static ValidationResult ValidateTimeframe(CostSettings settings)
    {
        settings.ApplyAutoTimeframe();

        if (settings.Timeframe == TimeframeType.Custom)
        {
            if (settings.GetFromDate() > settings.GetToDate())
            {
                return ValidationResult.Error("The from date must be before the to date.");
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Prints version information when debug mode is enabled.
    /// </summary>
    public static void PrintVersionIfDebug(bool debug)
    {
        if (debug)
            AnsiConsole.WriteLine($"Version: {typeof(CommandHelpers).Assembly.GetName().Version}");
    }

    /// <summary>
    /// Checks whether total cost exceeds the configured threshold.
    /// Returns exit code 1 if exceeded, 0 otherwise. Writes a warning to stderr when exceeded.
    /// </summary>
    public static int CheckCostThreshold(double totalCost, double? threshold, string currency)
    {
        if (threshold.HasValue && totalCost > threshold.Value)
        {
            Console.Error.WriteLine(
                $"Cost threshold exceeded: {totalCost:N2} {currency} > {threshold.Value:N2} {currency}");
            return 1;
        }

        return 0;
    }
}
