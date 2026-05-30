using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Threshold;

/// <summary>
/// Abstract base class for all threshold sub-commands. Handles common validation and setup.
/// </summary>
public abstract class BaseThresholdCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : ThresholdSettings
{
    protected readonly ICostRetriever CostRetriever;
    protected readonly Dictionary<OutputFormat, BaseOutputFormatter> OutputFormatters =
        OutputFormatterFactory.Create();

    protected BaseThresholdCommand(ICostRetriever costRetriever)
    {
        CostRetriever = costRetriever;
    }

    protected override ValidationResult Validate(CommandContext context, TSettings settings)
    {
        var subResult = CommandHelpers.ValidateAndResolveSubscription(
            settings.Subscription, settings.GetScope.IsSubscriptionBased,
            id => settings.Subscription = id);
        if (!subResult.Successful) return subResult;

        if (settings.Percentage == null && settings.FixedAmount == null)
            return ValidationResult.Error(
                "At least one threshold must be specified: --percentage or --fixed-amount.");

        return CommandHelpers.ValidateTimeframe(settings);
    }

    protected bool IsExceeded(double change, double? absoluteChange, ThresholdSettings settings)
    {
        if (settings.Percentage.HasValue && Math.Abs(change) > settings.Percentage.Value)
            return true;
        if (settings.FixedAmount.HasValue && absoluteChange.HasValue &&
            Math.Abs(absoluteChange.Value) > settings.FixedAmount.Value)
            return true;
        return false;
    }
}
