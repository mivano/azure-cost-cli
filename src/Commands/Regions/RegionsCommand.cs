using AzureCostCli.Commands.CostByResource;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Regions;

public class RegionsCommand: AsyncCommand<RegionsSettings>
{
    private readonly IRegionsRetriever _regionsRetriever;

    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = new();

    public RegionsCommand(IRegionsRetriever regionsRetriever)
    {
        _regionsRetriever = regionsRetriever;
        
        // Add the output formatters
        _outputFormatters.Add(OutputFormat.Console, new ConsoleOutputFormatter());
        _outputFormatters.Add(OutputFormat.Json, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Jsonc, new JsonOutputFormatter());
        _outputFormatters.Add(OutputFormat.Text, new TextOutputFormatter());
        _outputFormatters.Add(OutputFormat.Markdown, new MarkdownOutputFormatter());
        _outputFormatters.Add(OutputFormat.Csv, new CsvOutputFormatter());
    }

    public override async Task<int> ExecuteAsync(CommandContext context, RegionsSettings settings)
    {
        // Show version
        if (settings.Debug)
            AnsiConsole.WriteLine($"Version: {typeof(CostByResourceCommand).Assembly.GetName().Version}");

        var regions = await _regionsRetriever.RetrieveRegions();
        
        // Write the output
        await _outputFormatters[settings.Output]
             .WriteRegions(settings, regions);

        return 0;
    }
    
    
}
