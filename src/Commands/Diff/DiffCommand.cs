using System.Text.Json;
using AzureCostCli.CostApi;
using AzureCostCli.OutputFormatters;
using Spectre.Console;
using Spectre.Console.Cli;

using StatusContext = AzureCostCli.OutputFormatters.SpectreConsole.StatusContext;

namespace AzureCostCli.Commands.Diff;

public class DiffCommand : AsyncCommand<DiffSettings>
{
    private readonly ICostRetriever _costRetriever;
    private readonly Dictionary<OutputFormat, BaseOutputFormatter> _outputFormatters = OutputFormatterFactory.Create();

    public DiffCommand(ICostRetriever costRetriever)
    {
        _costRetriever = costRetriever;
    }

    protected override ValidationResult Validate(CommandContext context, DiffSettings settings)
    {
        // Automatically set timeframe to Custom if both from and to dates are provided
        settings.ApplyAutoTimeframe();
        
        // Validate if the timeframe is set to Custom, then the from date must be before the to date
        if (settings.Timeframe == TimeframeType.Custom)
        {
            if (settings.GetFromDate() > settings.GetToDate())
            {
                return ValidationResult.Error("The from date must be before the to date.");
            }
        }

        // Check for conflicting modes: both file params and source dates
        if (settings.HasFileParams && settings.HasSourceDates)
        {
            return ValidationResult.Error(
                "Cannot use both file-based comparison (--compare-to/--compare-from) and live comparison (--source-from/--source-to) at the same time. Please use one mode or the other.");
        }

        // Live comparison mode
        if (settings.HasSourceDates)
        {
            if (settings.SourceFrom!.Value > settings.SourceTo!.Value)
            {
                return ValidationResult.Error("The --source-from date must be before the --source-to date.");
            }

            var subResult = CommandHelpers.ValidateAndResolveSubscription(
                settings.Subscription, settings.GetScope.IsSubscriptionBased,
                id => settings.Subscription = id);
            if (!subResult.Successful) return subResult;

            return ValidationResult.Success();
        }

        // Partial source dates
        if (settings.SourceFrom.HasValue != settings.SourceTo.HasValue)
        {
            return ValidationResult.Error(
                "Both --source-from and --source-to must be provided for live comparison.");
        }

        // File-based comparison mode
        const string CompareToMessage =
            "The compare to file does not exist or is not specified. Please create the json file by running `azure-cost accumulatedCost -o json > filename.json`";
        const string CompareFromMessage =
            "The compare from file does not exist or is not specified. Please create the json file by running `azure-cost accumulatedCost -o json > filename.json`";

        if (string.IsNullOrEmpty(settings.CompareTo))
        {
            return ValidationResult.Error(CompareToMessage);
        }

        if (settings.CompareTo.EndsWith(".json") == false)
        {
            return ValidationResult.Error(CompareToMessage);
        }

        if (Path.Exists(settings.CompareTo) == false)
        {
            return ValidationResult.Error(CompareToMessage);
        }

        if (string.IsNullOrEmpty(settings.CompareFrom))
        {
            return ValidationResult.Error(CompareFromMessage);
        }

        if (settings.CompareFrom.EndsWith(".json") == false)
        {
            return ValidationResult.Error(CompareFromMessage);
        }

        if (Path.Exists(settings.CompareFrom) == false)
        {
            return ValidationResult.Error(CompareFromMessage);
        }


        return ValidationResult.Success();
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, DiffSettings settings, CancellationToken cancellationToken)
    {
        CommandHelpers.PrintVersionIfDebug(settings.Debug);

        AccumulatedCostDetails accumulatedCostSource = null;
        AccumulatedCostDetails accumulatedCostTarget = null;

        if (settings.HasSourceDates)
        {
            // Live comparison mode: fetch both periods from Azure Cost API
            _costRetriever.CostApiAddress = settings.CostApiAddress;
            _costRetriever.HttpTimeout = TimeSpan.FromSeconds(settings.HttpTimeout);

            await AnsiConsoleExt.StatusAsync(settings.Quiet, "Fetching cost data...", async ctx =>
                {
                    Subscription subscription = null;

                    if (settings.GetScope.IsSubscriptionBased)
                    {
                        ctx.Status = "Fetching subscription details...";
                        subscription = await _costRetriever.RetrieveSubscription(settings.Debug, settings.Subscription!.Value);
                    }
                    else
                    {
                        var enrollmentIdDisplayName = settings.EnrollmentAccountId != null ? $" {settings.EnrollmentAccountId}" : "";
                        var billingIdDisplayName = settings.BillingAccountId != null ? $" {settings.BillingAccountId}" : "";
                        subscription = new Subscription(string.Empty, string.Empty, Array.Empty<object>(), settings.GetScope.Name, settings.GetScope.Name, $"{settings.GetScope.Name} {enrollmentIdDisplayName} {billingIdDisplayName}", "Active", new SubscriptionPolicies(string.Empty, string.Empty, string.Empty));
                    }

                    ctx.Status = "Fetching source period costs...";
                    accumulatedCostSource = await FetchAccumulatedCostDetails(settings, subscription,
                        settings.SourceFrom!.Value, settings.SourceTo!.Value, ctx);

                    ctx.Status = "Fetching target period costs...";
                    accumulatedCostTarget = await FetchAccumulatedCostDetails(settings, subscription,
                        settings.GetFromDate(), settings.GetToDate(), ctx);
                });
        }
        else
        {
            // File-based comparison mode
            await AnsiConsoleExt.StatusAsync(settings.Quiet, "Reading data", async ctx =>
                {
                    accumulatedCostSource = await ReadAccumulatedCost(settings.CompareFrom);
                    accumulatedCostTarget = await ReadAccumulatedCost(settings.CompareTo);
                });
        }

        // Write the output
        await _outputFormatters[settings.Output]
            .WriteAccumulatedDiffCost(settings, accumulatedCostSource, accumulatedCostTarget);

        return 0;
    }

    private async Task<AccumulatedCostDetails> FetchAccumulatedCostDetails(DiffSettings settings,
        Subscription subscription, DateOnly fromDate, DateOnly toDate, StatusContext ctx)
    {
        var costs = await _costRetriever.RetrieveCosts(settings.Debug, settings.GetScope,
            settings.Filter, settings.Metric, TimeframeType.Custom, fromDate, toDate);

        List<CostItem> forecastedCosts = new List<CostItem>();

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (toDate >= today)
        {
            DateOnly forecastEndDate = new DateOnly(toDate.Year, toDate.Month,
                DateTime.DaysInMonth(toDate.Year, toDate.Month));

            ctx.Status = "Fetching forecasted cost data...";
            forecastedCosts = (await _costRetriever.RetrieveForecastedCosts(settings.Debug, settings.GetScope,
                settings.Filter, settings.Metric, TimeframeType.Custom, today, forecastEndDate)).ToList();
        }

        IEnumerable<CostNamedItem> bySubscriptionCosts = null;
        if (settings.GetScope.IsSubscriptionBased == false)
        {
            ctx.Status = "Fetching cost data by subscription...";
            bySubscriptionCosts = await _costRetriever.RetrieveCostBySubscription(settings.Debug,
                settings.GetScope, settings.Filter, settings.Metric, TimeframeType.Custom, fromDate, toDate);
        }

        ctx.Status = "Fetching cost data by service name...";
        var byServiceNameCosts = await _costRetriever.RetrieveCostByServiceName(settings.Debug,
            settings.GetScope, settings.Filter, settings.Metric, TimeframeType.Custom, fromDate, toDate);

        ctx.Status = "Fetching cost data by location...";
        var byLocationCosts = await _costRetriever.RetrieveCostByLocation(settings.Debug, settings.GetScope,
            settings.Filter, settings.Metric, TimeframeType.Custom, fromDate, toDate);

        ctx.Status = "Fetching cost data by resource group...";
        var byResourceGroupCosts = await _costRetriever.RetrieveCostByResourceGroup(settings.Debug,
            settings.GetScope, settings.Filter, settings.Metric, TimeframeType.Custom, fromDate, toDate);

        return new AccumulatedCostDetails(subscription, null, costs, forecastedCosts, byServiceNameCosts,
            byLocationCosts, byResourceGroupCosts, bySubscriptionCosts);
    }

    private async Task<AccumulatedCostDetails> ReadAccumulatedCost(string file)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
            var content = await File.ReadAllTextAsync(file);
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var cost = root.GetProperty("cost").Deserialize<List<CostDetail>>(options);
            var forecastedCosts = root.GetProperty("forecastedCosts").Deserialize<List<CostDetail>>(options);
            
            // Use JsonElement to directly access the correct property names in each object
            var byServiceNames = root.GetProperty("byServiceNames").EnumerateArray()
                .Select(item => new CostNamedItem(
                    item.GetProperty("ServiceName").GetString(),
                    item.GetProperty("Cost").GetDouble(),
                    item.GetProperty("CostUsd").GetDouble(),
                    item.GetProperty("Currency").GetString()))
                .ToList();

            var byLocationCosts = root.GetProperty("ByLocation").EnumerateArray()
                .Select(item => new CostNamedItem(
                    item.GetProperty("Location").GetString(),
                    item.GetProperty("Cost").GetDouble(),
                    item.GetProperty("CostUsd").GetDouble(),
                    item.GetProperty("Currency").GetString()))
                .ToList();

            var byResourceGroupCosts = root.GetProperty("ByResourceGroup").EnumerateArray()
                .Select(item => new CostNamedItem(
                    item.GetProperty("ResourceGroup").GetString(),
                    item.GetProperty("Cost").GetDouble(),
                    item.GetProperty("CostUsd").GetDouble(),
                    item.GetProperty("Currency").GetString()))
                .ToList();
           
            return new AccumulatedCostDetails(null, null,
                Costs: cost.Select(a => new CostItem(a.Date, a.Cost, a.CostUsd, a.Currency)).ToList(),
                ForecastedCosts: forecastedCosts.Select(a => new CostItem(a.Date, a.Cost, a.CostUsd, a.Currency))
                    .ToList(),
                ByServiceNameCosts: byServiceNames
                    .Select(a => new CostNamedItem(a.ItemName, a.Cost, a.CostUsd, a.Currency)).ToList(),
                ByLocationCosts: byLocationCosts
                    .Select(a => new CostNamedItem(a.ItemName, a.Cost, a.CostUsd, a.Currency)).ToList(),
                ByResourceGroupCosts: byResourceGroupCosts
                    .Select(a => new CostNamedItem(a.ItemName, a.Cost, a.CostUsd, a.Currency)).ToList(),
                null);
        }
        catch (Exception e)
        {
            throw new Exception($"Error reading the accumulated cost file: {file}", e);
        }
    }
}

internal class CostDetail
{
    public DateOnly Date { get; set; }
    public string ItemName { get; set; }
    public double Cost { get; set; }
    public double CostUsd { get; set; }
    public string Currency { get; set; }
}