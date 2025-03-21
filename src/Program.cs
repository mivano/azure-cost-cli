using System.ComponentModel;
using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.Commands.Budgets;
using AzureCostCli.Commands.CostByResource;
using AzureCostCli.Commands.CostByTag;
using AzureCostCli.Commands.DailyCost;
using AzureCostCli.Commands.DetectAnomaly;
using AzureCostCli.Commands.Diff;
using AzureCostCli.Commands.Regions;
using AzureCostCli.Commands.WhatIf;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using AzureCostCli.Infrastructure.TypeConvertors;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

// Setup the DI
var registrations = new ServiceCollection();

// Register a http client so we can make requests to the Azure Cost API
registrations.AddHttpClient("CostApi", client =>
{
  client.BaseAddress = new Uri("https://management.azure.com/");
  client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddPolicyHandler(PollyExtensions.GetRetryAfterPolicy());

// And one for the price API
registrations.AddHttpClient("PriceApi", client =>
{
  client.BaseAddress = new Uri("https://prices.azure.com/");
  client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddPolicyHandler(PollyExtensions.GetRetryAfterPolicy());

registrations.AddHttpClient("RegionsApi", client =>
{
  client.BaseAddress = new Uri("https://datacenters.microsoft.com/");
  client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddPolicyHandler(PollyPolicyExtensions.GetRetryAfterPolicy());


registrations.AddTransient<ICostRetriever, AzureCostApiRetriever>();
registrations.AddTransient<IPriceRetriever, AzurePriceRetriever>();
registrations.AddTransient<IRegionsRetriever, AzureRegionsRetriever>();

var registrar = new TypeRegistrar(registrations);

#if NET6_0
TypeDescriptor.AddAttributes(typeof(DateOnly), new TypeConverterAttribute(typeof(DateOnlyTypeConverter)));
#endif

// Setup the application itself
var app = new CommandApp(registrar);

// We default to the ShowCommand
app.SetDefaultCommand<AccumulatedCostCommand>();

app.Configure(config =>
{
  config.SetApplicationName("azure-cost");

  config.AddExample(new[] { "accumulatedCost", "-s", "00000000-0000-0000-0000-000000000000" });
  config.AddExample(new[] { "accumulatedCost", "-o", "json" });
  config.AddExample(new[] { "costByResource", "-s", "00000000-0000-0000-0000-000000000000", "-o", "text" });
  config.AddExample(new[] { "dailyCosts", "--dimension", "MeterCategory" });
  config.AddExample(new[] { "budgets", "-s", "00000000-0000-0000-0000-000000000000" });
  config.AddExample(new[] { "detectAnomalies", "--dimension", "ResourceId", "--recent-activity-days", "4" });
  config.AddExample(new[] { "costByTag", "--tag", "cost-center" });
  
  config.SetExceptionHandler((ex, resolver) =>
  {
    // Explicitly write to error output
    Console.Error.WriteLine(ex);
    return -1;
  });

  config.AddCommand<AccumulatedCostCommand>("accumulatedCost")
      .WithDescription("Show the accumulated cost details.");
  
  config.AddCommand<DailyCostCommand>("dailyCosts")
    .WithDescription("Show the daily cost by a given dimension.");
  
  config.AddCommand<CostByResourceCommand>("costByResource")
    .WithDescription("Show the cost details by resource.");

  config.AddCommand<CostByTagCommand>("costByTag")
    .WithDescription("Show the cost details by the provided tag key(s).");
  
  config.AddCommand<DetectAnomalyCommand>("detectAnomalies")
    .WithDescription("Detect anomalies and trends.");
  
  config.AddCommand<DiffCommand>("diff")
    .WithDescription("Show the cost difference between two timeframes.");
  
  config.AddCommand<BudgetsCommand>("budgets")
    .WithDescription("Get the available budgets.");
  
  // Disable for now
  // config.AddBranch<PricesSettings>("prices", add =>
  // {
  //   add.AddCommand<ListPricesCommand>("list").WithDescription("List prices");
  //   add.SetDescription("Use the Azure Price catalog");
  //   add.HideBranch();
  // });
  
  config.AddBranch<WhatIfSettings>("what-if", add =>
  {
   // add.AddCommand<DevTestWhatIfCommand>("devtest").WithDescription("Run what-if scenarios for DevTest subscriptions");
    add.AddCommand<RegionWhatIfCommand>("region").WithDescription("Run what-if scenarios to check price differences if the resources would have run in a different region. Only applies to VMs.");
    add.SetDescription("Run what-if scenarios");
  });
  
  config.AddCommand<RegionsCommand>("regions")
    .WithDescription("Get the available Azure regions.");

  
  config.ValidateExamples();
});

// Run the application
return await app.RunAsync(args);