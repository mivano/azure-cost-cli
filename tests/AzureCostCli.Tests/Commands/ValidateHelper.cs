using Spectre.Console;
using Spectre.Console.Cli;

namespace AzureCostCli.Tests.Commands;

/// <summary>
/// Calls Validate on Spectre.Console.Cli commands. Validate was protected in Spectre.Console.Cli
/// 0.55.0 and is public again since 0.57.x; this helper keeps the call sites in the tests stable.
/// </summary>
public static class ValidateHelper
{
    public static ValidationResult CallValidate<TSettings>(AsyncCommand<TSettings> command,
        CommandContext context, TSettings settings) where TSettings : CommandSettings
        => command.Validate(context, settings);
}
