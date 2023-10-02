using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.Commands.ShowCommand.OutputFormatters;
using AzureCostCli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Threshold;

public abstract class BaseThresholdCommand : AsyncCommand<ThresholdSettings> 
{

    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = new();
    
    protected BaseThresholdCommand()
    {
        // Add the output formatters
        _outputFormatters.Add(OutputFormat.Console, new ConsoleOutputFormatter());
        _outputFormatters.Add(OutputFormat.Json, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Jsonc, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Text, new TextOutputFormatter());
        _outputFormatters.Add(OutputFormat.Markdown, new MarkdownOutputFormatter());
        _outputFormatters.Add(OutputFormat.Csv, new CsvOutputFormatter());
    }
    
    protected abstract Task<ThresholdResult> PerformThresholdCheck(CommandContext context, Guid subscriptionId,
        ThresholdSettings settings);

    public override async Task<int> ExecuteAsync(CommandContext context, ThresholdSettings settings)
    {
        // Show version
        if (settings.Debug)
            AnsiConsole.WriteLine($"Version: {typeof(AccumulatedCostCommand).Assembly.GetName().Version}");
        
        // Get the subscription ID from the settings
        var subscriptionId = settings.Subscription;

        if (subscriptionId == Guid.Empty)
        {
            // Get the subscription ID from the Azure CLI
            try
            {
                if (settings.Debug)
                    AnsiConsole.WriteLine(
                        "No subscription ID specified. Trying to retrieve the default subscription ID from Azure CLI.");

                subscriptionId = Guid.Parse(AzCommand.GetDefaultAzureSubscriptionId());

                if (settings.Debug)
                    AnsiConsole.WriteLine($"Default subscription ID retrieved from az cli: {subscriptionId}");

                settings.Subscription = subscriptionId;
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(new ArgumentException(
                    "Missing subscription ID. Please specify a subscription ID or login to Azure CLI.", e));
                return -1;
            }
        }
        
        ThresholdResult result = await PerformThresholdCheck(context, subscriptionId, settings);
        
        // Write the output
        await _outputFormatters[settings.Output]
            .WriteThreshold(settings, result);

        // Output and fail logic
        return result.IsThresholdExceeded && settings.FailOnError ? -1 : 0;
    }
}