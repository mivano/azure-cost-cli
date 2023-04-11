using System.Runtime.CompilerServices;
using Spectre.Console.Cli;

var app = new CommandApp();

app.SetDefaultCommand<ShowCommand>();

app.Configure(config =>
{
    config.SetApplicationName("Azure Cost");
    config.ValidateExamples();
    config.AddCommand<ShowCommand>("show").WithDescription("Show the costs for a subscription");
    
});

return app.Run(args);