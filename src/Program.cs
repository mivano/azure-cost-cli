using System.ComponentModel;
using AzureCostCli.Commands;
using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.Commands.Budgets;
using AzureCostCli.Commands.CostByResource;
using AzureCostCli.Commands.CostByTag;
using AzureCostCli.Commands.DailyCost;
using AzureCostCli.Commands.DetectAnomaly;
using AzureCostCli.Commands.Regions;
using AzureCostCli.Commands.ShowCommand;
using AzureCostCli.Commands.Threshold;
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
}).AddPolicyHandler(PollyPolicyExtensions.GetRetryAfterPolicy());

registrations.AddHttpClient("RegionsApi", client =>
{
  client.BaseAddress = new Uri("https://datacenters.microsoft.com/");
  client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddPolicyHandler(PollyPolicyExtensions.GetRetryAfterPolicy());


registrations.AddTransient<ICostRetriever, AzureCostApiRetriever>();
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
  
#if DEBUG
  config.PropagateExceptions();
#endif

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
  
  config.AddCommand<BudgetsCommand>("budgets")
    .WithDescription("Get the available budgets.");
  
  config.AddCommand<RegionsCommand>("regions")
    .WithDescription("Get the available Azure regions.");
  
  config.AddBranch<ThresholdSettings>("threshold", add =>
  {
    add.AddCommand<DailyChangeThresholdCommand>("daily-change").WithDescription("Analyzes the difference between today's and yesterday's costs to check if a defined threshold is exceeded.");
    add.AddCommand<WeeklyAverageThresholdCommand>("weekly-average").WithDescription("Compares today's cost against the average cost of the past week to identify significant deviations.");
    add.AddCommand<ForecastDeviationThresholdCommand>("forecast-deviation").WithDescription("Assesses the deviation between today's actual cost and the forecasted cost for the period to identify unexpected cost surges.");
    add.AddCommand<ServiceSpikeThresholdCommand>("service-spike").WithDescription("Monitors cost spikes in individual Azure services by comparing today's costs against an aggregated previous period.");
    add.SetDescription("Monitors and analyzes Azure cost metrics to identify deviations, trends, and potential issues based on specified thresholds.");
    add.AddExample(new[] { "threshold", "weekly-average", "--fixed-amount", "100" });
    add.AddExample(new[] { "threshold", "daily-change","--percentage", "10" });
  });

  
  config.ValidateExamples();
});

// Run the application
return await app.RunAsync(args);