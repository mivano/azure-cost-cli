using System.Diagnostics;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

public class ShowCommand : AsyncCommand<ShowSettings>
{
    private readonly ICostRetriever _costRetriever;

    private readonly Dictionary<OutputFormat, OutputFormatter> _outputFormatters = new();

    public ShowCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;
        
        // Add the output formatters
        _outputFormatters.Add(OutputFormat.Console, new ConsoleOutputFormatter());
        _outputFormatters.Add(OutputFormat.Json, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Text, new TextOutputFormatter());
    }

    public override ValidationResult Validate(CommandContext context, ShowSettings settings)
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

    public override async Task<int> ExecuteAsync(CommandContext context, ShowSettings settings)
    {
        // Show version
        if (settings.Debug)
            AnsiConsole.WriteLine($"Version: {typeof(ShowCommand).Assembly.GetName().Version}");
        
      
        // Get the subscription ID from the settings
        var subscriptionId = settings.Subscription;

        if (subscriptionId == Guid.Empty)
        {
            // Get the subscription ID from the Azure CLI
            try
            {
                if (settings.Debug)
                    AnsiConsole.WriteLine("No subscription ID specified. Trying to retrieve the default subscription ID from Azure CLI.");
                
                subscriptionId = Guid.Parse(GetDefaultAzureSubscriptionId());
                
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
        var costs = await _costRetriever.RetrieveCosts(settings.Debug, subscriptionId, settings.Timeframe,
            settings.From, settings.To);
        var forecastedCosts = await _costRetriever.RetrieveForecastedCosts(settings.Debug, subscriptionId);
        var byServiceNameCosts =  await _costRetriever.RetrieveCostByServiceName(settings.Debug,
            subscriptionId, settings.Timeframe, settings.From, settings.To);
        var byLocationCosts =  await _costRetriever.RetrieveCostByLocation(settings.Debug, subscriptionId,
            settings.Timeframe, settings.From, settings.To);
        var byResourceGroupCosts =  await _costRetriever.RetrieveCostByResourceGroup(settings.Debug, subscriptionId,
            settings.Timeframe, settings.From, settings.To);

      
        // Write the output
        await _outputFormatters[settings.Output]
            .WriteOutput(settings, costs, forecastedCosts, byServiceNameCosts, byLocationCosts, byResourceGroupCosts);

        return 0;
    }

   

    static string GetDefaultAzureSubscriptionId()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "az",
            Arguments = "account show",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = startInfo })
        {
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                throw new Exception($"Error executing 'az account show': {error}");
            }

            using (var jsonDocument = JsonDocument.Parse(output))
            {
                JsonElement root = jsonDocument.RootElement;
                if (root.TryGetProperty("id", out JsonElement idElement))
                {
                    string subscriptionId = idElement.GetString();
                    return subscriptionId;
                }
                else
                {
                    throw new Exception("Unable to find the 'id' property in the JSON output.");
                }
            }
        }
    }
}