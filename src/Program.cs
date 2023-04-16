
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

// Setup the DI
var registrations = new ServiceCollection();

// Register a http client so we can make requests to the Azure Cost API
registrations.AddHttpClient("CostApi", client =>
{
  client.BaseAddress = new Uri("https://management.azure.com/");
  client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddPolicyHandler(AzureCostApiRetriever.GetRetryAfterPolicy());

registrations.AddTransient<ICostRetriever, AzureCostApiRetriever>();

var registrar = new TypeRegistrar(registrations);

// Setup the application itself
var app = new CommandApp(registrar);

// We default to the ShowCommand
app.SetDefaultCommand<ShowCommand>();

app.Configure(config =>
{
  config.SetApplicationName("azure-cost");

  config.AddExample(new[] { "show", "-s", "00000000-0000-0000-0000-000000000000" });
  config.AddExample(new[] { "show", "-s", "00000000-0000-0000-0000-000000000000", "-o", "json" });
  config.AddExample(new[] { "show", "-s", "00000000-0000-0000-0000-000000000000", "-o", "text" });

#if DEBUG
  config.PropagateExceptions();
#endif

  config.AddCommand<ShowCommand>("show")
      .WithDescription("Show the cost details for a subscription.");
  
  config.ValidateExamples();
});

// Run the application
return await app.RunAsync(args);