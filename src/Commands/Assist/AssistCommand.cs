using System.Text;
using AzureCostCli.Commands.CostByResource;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using OpenAI.GPT3.Interfaces;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;
using OpenAI.GPT3.ObjectModels.SharedModels;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands.Assist;

public class AssistCommand : AsyncCommand<AssistSettings>
{
    private readonly ICostRetriever _costRetriever;
    private readonly IOpenAIService _openAiService;

    readonly List<ChatMessage> _messages = new();

    public AssistCommand(ICostRetriever costRetriever, IOpenAIService openAiService)
    {
        _costRetriever = costRetriever;
        _openAiService = openAiService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, AssistSettings settings)
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

        IEnumerable<CostResourceItem> resources = null;

        AnsiConsole.Write(new FigletText("Azure Cost CLI AI"));
        await AnsiConsole.Status()
            .StartAsync("Setting up the model...", async ctx =>
            {
                ctx.Status("Retrieving cost for resources...");
                resources = await _costRetriever.RetrieveCostForResources(settings.Debug, subscriptionId,
                    settings.Timeframe,
                    settings.From, settings.To);

                ctx.Status("Retrieving cost for resources... Done");

                ctx.Status("Retrieving previous cost for resources...");
                var previousCost = await _costRetriever.RetrieveCosts(settings.Debug, subscriptionId,
                    settings.Timeframe,
                    settings.From, settings.To);

                ctx.Status("Retrieving forecasted cost...");
                DateOnly forecastStartDate;
                DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
                List<CostItem> forecastedCosts = new List<CostItem>();
                switch (settings.Timeframe)
                {
                    case TimeframeType.BillingMonthToDate:
                    case TimeframeType.MonthToDate:
                        forecastStartDate =
                            DateOnly.FromDateTime(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
                        break;
                    case TimeframeType.TheLastBillingMonth:
                    case TimeframeType.TheLastMonth:
                        forecastStartDate =
                            DateOnly.FromDateTime(
                                new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1));
                        break;
                    case TimeframeType.WeekToDate:
                        forecastStartDate =
                            DateOnly.FromDateTime(DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek));
                        break;
                    default:
                        // Custom Timeframe
                        forecastStartDate = settings.To >= today ? today : default;
                        break;
                }

                if (forecastStartDate != default)
                {
                    DateOnly forecastEndDate = new DateOnly(settings.To.Year, settings.To.Month,
                        DateTime.DaysInMonth(settings.To.Year, settings.To.Month));

                    forecastedCosts = (await _costRetriever.RetrieveForecastedCosts(settings.Debug, subscriptionId,
                        TimeframeType.Custom,
                        forecastStartDate,
                        forecastEndDate)).ToList();
                }

                ctx.Status("Done retrieving cost data");

                ctx.Status("Setting up the AI model...");
                // Prepare the AI
                StackMessages(new ChatMessage("system",
                    "You are an Cloud Cost assistant, here to help with Azure cost data. "));

                StackMessages(new ChatMessage("system",
                    "The scanned subscription contained the following resources, seperated by comma, each having a resource name, type, location, group, service name and cost: "));

                // Setup the resources
                var sb = new StringBuilder();
                foreach (var resource in resources.OrderByDescending(a => a.Cost))
                {
                    sb.Append(
                        $"{resource.ResourceId.Split('/').Last()},{resource.ResourceType},{resource.ResourceLocation},{resource.ResourceGroupName},{resource.ServiceName},{(settings.UseUSD ? resource.CostUSD : resource.Cost):N2} {(settings.UseUSD ? "USD" : resource.Currency)}");

                    if (sb.Length > 4000)
                    {
                        StackMessages(new ChatMessage("system", sb.ToString()));
                        sb.Clear();
                    }
                }

                if (sb.Length > 0)
                {
                    StackMessages(new ChatMessage("system", sb.ToString()));
                    sb.Clear();
                }

                StackMessages(new ChatMessage("system",
                    "Cost per day was as follows: " + string.Join(',',
                        previousCost.Select(a =>
                            $"{a.Date:yyyy-MM-dd}: {(settings.UseUSD ? a.CostUsd : a.Cost):N2} {(settings.UseUSD ? "USD" : a.Currency)}"))));
                StackMessages(new ChatMessage("system",
                    "Forecasted cost per day is as follows: " + string.Join(',',
                        forecastedCosts.Select(a =>
                            $"{a.Date:yyyy-MM-dd}: {(settings.UseUSD ? a.CostUsd : a.Cost):N2} {(settings.UseUSD ? "USD" : a.Currency)}"))));

                ctx.Status("Done setting up the AI model");
            });


        AnsiConsole.MarkupLine(
            ":waving_hand: Hello! How can I assist you with your Azure cost data today :money_bag:? You can ask me about cost trends :bar_chart:, anomalies :hospital:, cost forecasting :chart_increasing:, or ways to optimize your costs. Enter `exit` to quit");

        string question;
        do
        {
            question = AnsiConsole.Ask<string>("[green]What is your question?[/]");
            if (string.Equals(question, "exit", StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals(question, "quit", StringComparison.InvariantCultureIgnoreCase))
            {
                AnsiConsole.MarkupLine("Thanks for using Azure Cost CLI AI. Goodbye!");
                break;
            }

            var chatMsg = new ChatMessage("user", question);

            StackMessages(chatMsg);

            await AnsiConsole.Status()
                .StartAsync("Asking the question...", async ctx =>
                {
                    var completionResult = await _openAiService.ChatCompletion.CreateCompletion(
                        new ChatCompletionCreateRequest
                        {
                            Messages = _messages.ToArray(),
                            Model = Models.ChatGpt3_5Turbo,
                        });

                    if (completionResult.Successful && completionResult.Choices.Any())
                    {
                        AnsiConsole.WriteLine(completionResult.Choices.First().Message.Content);
                        var choices = completionResult.Choices;
                        var messages = ToCompletionMessage(choices);

                        //stack the response as well - everything is context to Open AI
                        StackMessages(messages);
                    }
                    else
                    {
                        AnsiConsole.WriteLine("Unable to perform AI call: " + 
                            completionResult.Error?.Message);
                    }
                });
        } while (true);

        return 0;
    }

    void StackMessages(params ChatMessage[] message)
    {
        _messages.AddRange(message);
    }

    static ChatMessage[] ToCompletionMessage(
        IEnumerable<ChatChoiceResponse> choices)
        => choices.Select(x => x.Message).ToArray();
}