using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzureCostCli.Commands;

public class LogCommandSettings : CommandSettings
{
    [CommandOption("--debug")]
    [Description("Increase logging verbosity to show all debug logs.")]
    [DefaultValue(false)]
    public bool Debug { get; set; }

    [CommandOption("--no-color")]
    [Description("Disable ANSI color output. Useful for CI/logging environments.")]
    [DefaultValue(false)]
    public bool NoColor { get; set; }

    [CommandOption("--quiet")]
    [Description("Suppress all status/progress messages. Only actual data output is written. Useful for scripting.")]
    [DefaultValue(false)]
    public bool Quiet { get; set; }
    
}