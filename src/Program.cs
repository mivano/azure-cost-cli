using AzureCostCli.Commands;
using AzureCostCli.Commands.Assist;
using AzureCostCli.Commands.CostByResource;
using AzureCostCli.Commands.ShowCommand;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.GPT3.Extensions;
using Spectre.Console.Cli;

// Setup the DI
var registrations = new ServiceCollection();

// Register a http client so we can make requests to the Azure Cost API
registrations.AddHttpClient("CostApi", client =>
{
  client.BaseAddress = new Uri("https://management.azure.com/");
  client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddPolicyHandler(AzureCostApiRetriever.GetRetryAfterPolicy());

var configuration = new ConfigurationManager();

configuration.AddEnvironmentVariables();
configuration.AddUserSecrets<Program>();
registrations.AddScoped<IConfiguration>(_ => configuration);
registrations.AddTransient<ICostRetriever, AzureCostApiRetriever>();
registrations.AddOpenAIService(); // You do need to have the key in your user secrets or environment variables

var registrar = new TypeRegistrar(registrations);

// Setup the application itself
var app = new CommandApp(registrar);

// We default to the ShowCommand
app.SetDefaultCommand<AccumulatedCostCommand>();

app.Configure(config =>
{
  config.SetApplicationName("azure-cost");

  config.AddExample(new[] { "accumulatedCost", "-s", "00000000-0000-0000-0000-000000000000" });
  config.AddExample(new[] { "accumulatedCost", "-s", "00000000-0000-0000-0000-000000000000", "-o", "json" });
  config.AddExample(new[] { "costByResource", "-s", "00000000-0000-0000-0000-000000000000", "-o", "text" });

#if DEBUG
  config.PropagateExceptions();
#endif

  config.AddCommand<AccumulatedCostCommand>("accumulatedCost")
      .WithDescription("Show the accumulated cost details.");
  
  config.AddCommand<CostByResourceCommand>("costByResource")
    .WithDescription("Show the cost details by resource.");
  
  config.AddCommand<AssistCommand>("assist")
    .WithDescription("AI Assist over your Azure cost.");
  
  config.ValidateExamples();
});

// Run the application
return await app.RunAsync(args);