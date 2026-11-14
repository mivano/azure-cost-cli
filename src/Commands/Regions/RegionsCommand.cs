using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Regions;

public class RegionsCommand: AsyncCommand<RegionsSettings>
{
    private readonly IRegionsRetriever _regionsRetriever;
    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = OutputFormatterFactory.Create();

    public RegionsCommand(IRegionsRetriever regionsRetriever)
    {
        _regionsRetriever = regionsRetriever;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, RegionsSettings settings, CancellationToken cancellationToken)
    {
        CommandHelpers.PrintVersionIfDebug(settings.Debug);

        var regions = await _regionsRetriever.RetrieveRegions();
        
        // Write the output
        await _outputFormatters[settings.Output]
             .WriteRegions(settings, regions);

        return 0;
    }
    
    
}
