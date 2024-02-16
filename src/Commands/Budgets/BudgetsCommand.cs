using AzureCostCli.Commands.CostByResource;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Budgets;

public class BudgetsCommand: AsyncCommand<BudgetsSettings>
{
    private readonly ICostRetriever _costRetriever;

    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = new();

    public BudgetsCommand(ICostRetriever costRetriever)
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

    public override async Task<int> ExecuteAsync(CommandContext context, BudgetsSettings settings)
    {
        // Show version
        if (settings.Debug)
            AnsiConsole.WriteLine($"Version: {typeof(CostByResourceCommand).Assembly.GetName().Version}");
        
      
        // Get the subscription ID from the settings
        var subscriptionId = settings.Subscription;

        if (subscriptionId.GetValueOrDefault() == Guid.Empty)
        {
            // Get the subscription ID from the Azure CLI
            try
            {
                if (settings.Debug)
                    AnsiConsole.WriteLine("No subscription ID specified. Trying to retrieve the default subscription ID from Azure CLI.");
                
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

        // Fetch the details from the Azure Cost Management API
        var budgets = await _costRetriever.RetrieveBudgets(settings.Debug, settings.GetScope,
            new SecurityCredentials(settings.TenantId, settings.ServicePrincipalId, settings.ServicePrincipalSecret));

        // Write the output
        await _outputFormatters[settings.Output]
            .WriteBudgets(settings, budgets);

        return 0;
    }
    
    
}
