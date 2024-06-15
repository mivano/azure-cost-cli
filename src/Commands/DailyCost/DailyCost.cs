using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;
using System;

namespace AzureCostCli.Commands.DailyCost;

public class DailyCostCommand : AsyncCommand<DailyCostSettings>
{
    private readonly ICostRetriever _costRetriever;

    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = new();

    public DailyCostCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;

        // Add the output formatters
        _outputFormatters.Add(OutputFormat.Console, new ConsoleOutputFormatter());
        _outputFormatters.Add(OutputFormat.Json, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Jsonc, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Text, new TextOutputFormatter());
        _outputFormatters.Add(OutputFormat.Markdown, new MarkdownOutputFormatter());
        _outputFormatters.Add(OutputFormat.Csv, new CsvOutputFormatter());
    }

    public override ValidationResult Validate(CommandContext context, DailyCostSettings settings)
    {
        // Validate if the timeframe is set to Custom, then the from and to dates must be specified and the from date must be before the to date
        if (settings.Timeframe == TimeframeType.Custom)
        {
            if (settings.From == null)
            {
                return ValidationResult.Error("The from date must be specified when the timeframe is set to Custom.");
            }

            if (settings.To == null)
            {
                return ValidationResult.Error("The to date must be specified when the timeframe is set to Custom.");
            }

            if (settings.From > settings.To)
            {
                return ValidationResult.Error("The from date must be before the to date.");
            }
        }

        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DailyCostSettings settings)
    {
        // Show version
        if (settings.Debug)
            AnsiConsole.WriteLine($"Version: {typeof(AccumulatedCostCommand).Assembly.GetName().Version}");


        _costRetriever.CostApiAddress = settings.CostApiAddress;
        _costRetriever.HttpTimeout = TimeSpan.FromSeconds(settings.HttpTimeout);

        // Get the subscription ID from the settings
        var subscriptionId = settings.Subscription;

        if (subscriptionId.HasValue == false && (settings.GetScope.IsSubscriptionBased))
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

        IEnumerable<CostDailyItem> dailyCost = new List<CostDailyItem>();

        // if output format is not csv, json, or jsonc, then don't include tags
        if (settings.Output != OutputFormat.Json &&
            settings.Output != OutputFormat.Jsonc &&
            settings.Output != OutputFormat.Csv)
        {
            settings.IncludeTags = false;
        }

        await AnsiConsoleExt.Status()
            .StartAsync("Fetching daily cost data...", async ctx =>
            {
                // Fetch the costs from the Azure Cost Management API

                dailyCost = await _costRetriever.RetrieveDailyCost(settings.Debug, settings.GetScope,
                    settings.Filter,
                    settings.Metric,
                    settings.Dimension,
                    settings.Timeframe,
                    settings.From, settings.To,
                    settings.IncludeTags);
            });

        // Write the output
        await _outputFormatters[settings.Output]
            .WriteDailyCost(settings, dailyCost);

        return 0; // Omitted
    }
}