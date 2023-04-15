using Spectre.Console.Cli;

var app = new CommandApp();

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

return await app.RunAsync(args);