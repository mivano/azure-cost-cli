using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Prices;

public class ListPricesCommand: AsyncCommand<PricesSettings>
{
    private readonly IPriceRetriever _priceRetriever;

    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = new();

    
    public ListPricesCommand(IPriceRetriever priceRetriever)
    {
        _priceRetriever = priceRetriever;
        
        // Add the output formatters
        _outputFormatters.Add(OutputFormat.Console, new ConsoleOutputFormatter());
        _outputFormatters.Add(OutputFormat.Json, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Jsonc, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Text, new TextOutputFormatter());
        _outputFormatters.Add(OutputFormat.Markdown, new MarkdownOutputFormatter());
        _outputFormatters.Add(OutputFormat.Csv, new CsvOutputFormatter());
    }
    
    public override async Task<int> ExecuteAsync(CommandContext context, PricesSettings settings)
    {
        _priceRetriever.PriceApiAddress = settings.PriceApiAddress;
        
        var prices = await _priceRetriever.GetAzurePricesAsync();
        
        return 0;
    }
}