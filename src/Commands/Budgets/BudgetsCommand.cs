using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Budgets;

public class BudgetsCommand : AsyncCommand<BudgetsSettings>
{
    private readonly ICostRetriever _costRetriever;
    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = OutputFormatterFactory.Create();

    public BudgetsCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;
    }

    public override ValidationResult Validate(CommandContext context, BudgetsSettings settings)
    {
        return CommandHelpers.ValidateAndResolveSubscription(
            settings.Subscription, settings.GetScope.IsSubscriptionBased,
            id => settings.Subscription = id);
    }

    public override async Task<int> ExecuteAsync(CommandContext context, BudgetsSettings settings, CancellationToken cancellationToken)
    {
        CommandHelpers.PrintVersionIfDebug(settings.Debug);

        _costRetriever.CostApiAddress = settings.CostApiAddress;
        _costRetriever.HttpTimeout = TimeSpan.FromSeconds(settings.HttpTimeout);

        // Fetch the details from the Azure Cost Management API
        var budgets = await _costRetriever.RetrieveBudgets(settings.Debug, settings.GetScope);

        // Write the output
        await _outputFormatters[settings.Output]
            .WriteBudgets(settings, budgets);

        return 0;
    }


}
