using AzureCostCli.Commands.CostByResource;
using AzureCostCli.Commands.ShowCommand.OutputFormatters;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.CostByTag;

public class CostByTagCommand : AsyncCommand<CostByTagSettings>
{
    private readonly ICostRetriever _costRetriever;

    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = new();

    public CostByTagCommand(ICostRetriever costRetriever)
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

    public override ValidationResult Validate(CommandContext context, CostByTagSettings settings)
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

    public override async Task<int> ExecuteAsync(CommandContext context, CostByTagSettings settings)
    {
        // Show version
        if (settings.Debug)
            AnsiConsole.WriteLine($"Version: {typeof(CostByResourceCommand).Assembly.GetName().Version}");


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

        // Fetch the costs from the Azure Cost Management API
        IEnumerable<CostResourceItem> resources = new List<CostResourceItem>();

        await AnsiConsole.Status()
            .StartAsync("Fetching cost data for resources...", async ctx =>
            {
                resources = await _costRetriever.RetrieveCostForResources(
                    settings.Debug,
                    subscriptionId, settings.Filter,
                    settings.Metric,
                    true,
                    settings.Timeframe,
                    settings.From,
                    settings.To);
            });

        var byTags = GetResourcesByTag(resources, settings.Tags.ToArray());

        // Write the output
        await _outputFormatters[settings.Output]
            .WriteCostByTag(settings, byTags);

        return 0;
    }

    private Dictionary<string, Dictionary<string, List<CostResourceItem>>> GetResourcesByTag(
        IEnumerable<CostResourceItem> resources, params string[] tags)
    {
        var resourcesByTag =
            new Dictionary<string, Dictionary<string, List<CostResourceItem>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            resourcesByTag[tag] = new Dictionary<string, List<CostResourceItem>>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var resource in resources)
        {
            foreach (var tag in tags)
            {
                var resourceTags = new Dictionary<string, string>(resource.Tags, StringComparer.OrdinalIgnoreCase);

                if (resourceTags.ContainsKey(tag))
                {
                    var tagValue = resourceTags[tag];
                    if (!resourcesByTag[tag].ContainsKey(tagValue))
                    {
                        resourcesByTag[tag][tagValue] = new List<CostResourceItem>();
                    }

                    resourcesByTag[tag][tagValue].Add(resource);
                }
            }
        }

        return resourcesByTag;
    }
}