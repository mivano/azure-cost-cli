using AzureCostCli.Commands;
using AzureCostCli.Commands.WhatIf;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Infrastructure;

/// <summary>
/// Intercepts every command invocation to apply config-file defaults before
/// the command executes. CLI-provided values always override config-file values.
/// Also applies --no-color from settings (so it works whether set via CLI or config file).
/// </summary>
public class ConfigFileInterceptor : ICommandInterceptor
{
    public void Intercept(CommandContext context, CommandSettings settings)
    {
        var config = ConfigFileLoader.Load();

        if (settings is CostSettings costSettings)
        {
            ConfigFileLoader.ApplyToSettings(costSettings, config);
            ApplyNoColor(costSettings.NoColor);
        }
        else if (settings is WhatIfSettings whatIfSettings)
        {
            ConfigFileLoader.ApplyToWhatIfSettings(whatIfSettings, config);
            ApplyNoColor(whatIfSettings.NoColor);
        }
    }

    private static void ApplyNoColor(bool noColor)
    {
        if (noColor)
        {
            AnsiConsole.Profile.Capabilities.ColorSystem = ColorSystem.NoColors;
            AnsiConsole.Profile.Capabilities.Ansi = false;
        }
    }
}
