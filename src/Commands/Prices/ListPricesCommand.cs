using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Prices;

public class ListPricesCommand: AsyncCommand<PricesSettings>
{
    private readonly IPriceRetriever _priceRetriever;

    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = OutputFormatterFactory.Create();

    public ListPricesCommand(IPriceRetriever priceRetriever)
    {
        _priceRetriever = priceRetriever;
    }
    
    protected override async Task<int> ExecuteAsync(CommandContext context, PricesSettings settings, CancellationToken cancellationToken)
    {
        _priceRetriever.PriceApiAddress = settings.PriceApiAddress;
        
        var prices = await _priceRetriever.GetAzurePricesAsync();
        
        return 0;
    }
}