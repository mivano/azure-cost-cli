using AzureCostCli.OutputFormatters.SpectreConsole;

namespace Spectre.Console;

public static partial class AnsiConsoleExt
{
    /// <summary>
    /// Creates a new <see cref="StatusExt"/> instance.
    /// </summary>
    /// <returns>A <see cref="StatusExt"/> instance.</returns>
    public static StatusExt Status()
    {
        return new StatusExt(AnsiConsole.Console);
    }
}