using AzureCostCli.Commands;
using Spectre.Console.Cli;

namespace AzureCostCli.Infrastructure;

/// <summary>
/// Intercepts every command invocation to apply config-file defaults before
/// the command executes. CLI-provided values always override config-file values.
/// </summary>
public class ConfigFileInterceptor : ICommandInterceptor
{
    public void Intercept(CommandContext context, CommandSettings settings)
    {
        if (settings is CostSettings costSettings)
        {
            var config = ConfigFileLoader.Load();
            ConfigFileLoader.ApplyToSettings(costSettings, config);
        }
    }
}
