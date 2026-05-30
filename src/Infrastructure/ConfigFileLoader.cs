using System.Text.Json;
using System.Text.Json.Serialization;
using AzureCostCli.Commands;
using AzureCostCli.Commands.WhatIf;

namespace AzureCostCli.Infrastructure;

/// <summary>
/// Reads and merges config from ~/.azure-cost-cli.json (global) and
/// .azure-cost-cli.json in the current directory (local).
/// The path can be overridden via the AZURE_COST_CLI_CONFIG environment variable.
/// Local values override global values.
/// </summary>
public static class ConfigFileLoader
{
    private const string ConfigFileName = ".azure-cost-cli.json";
    private const string EnvVarName = "AZURE_COST_CLI_CONFIG";

    public static ConfigFileValues Load()
    {
        var envPath = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return LoadFile(envPath) ?? new ConfigFileValues();
        }

        var globalPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ConfigFileName);

        var localPath = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);

        var global = LoadFile(globalPath);
        var local = LoadFile(localPath);

        return Merge(global, local);
    }

    private static ConfigFileValues? LoadFile(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ConfigFileValues>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
            });
        }
        catch
        {
            // Silently ignore malformed config files
            return null;
        }
    }

    private static ConfigFileValues Merge(ConfigFileValues? global, ConfigFileValues? local)
    {
        if (global == null) return local ?? new ConfigFileValues();
        if (local == null) return global;

        // Local overrides global (non-null local values win)
        return new ConfigFileValues
        {
            Subscription = local.Subscription ?? global.Subscription,
            Output = local.Output ?? global.Output,
            Timeframe = local.Timeframe ?? global.Timeframe,
            UseUSD = local.UseUSD ?? global.UseUSD,
            Metric = local.Metric ?? global.Metric,
            OthersCutoff = local.OthersCutoff ?? global.OthersCutoff,
        };
    }

    /// <summary>
    /// Applies config-file defaults into settings. CLI-provided values always win;
    /// a config value is only applied when the settings property still holds its
    /// programmatic default (i.e. the user did NOT pass a CLI flag for it).
    /// </summary>
    public static void ApplyToSettings(CostSettings settings, ConfigFileValues config)
    {
        // Subscription: only apply if the user didn't supply one
        if ((settings.Subscription == null || settings.Subscription == Guid.Empty) && config.Subscription != null)
            settings.Subscription = config.Subscription;

        // Output: only apply if still at the built-in default (Console)
        if (settings.Output == OutputFormat.Console && config.Output.HasValue)
            settings.Output = config.Output.Value;

        // Timeframe: only apply if still at the built-in default
        if (settings.Timeframe == TimeframeType.BillingMonthToDate && config.Timeframe.HasValue)
            settings.Timeframe = config.Timeframe.Value;

        // UseUSD: only apply if still false (default)
        if (!settings.UseUSD && config.UseUSD.HasValue)
            settings.UseUSD = config.UseUSD.Value;

        // Metric: only apply if still at the built-in default
        if (settings.Metric == MetricType.ActualCost && config.Metric.HasValue)
            settings.Metric = config.Metric.Value;

        // OthersCutoff: only apply if still at the built-in default (10)
        if (settings.OthersCutoff == 10 && config.OthersCutoff.HasValue)
            settings.OthersCutoff = config.OthersCutoff.Value;
    }

    /// <summary>
    /// Applies config-file defaults into WhatIfSettings. Same precedence rules as ApplyToSettings.
    /// </summary>
    public static void ApplyToWhatIfSettings(WhatIfSettings settings, ConfigFileValues config)
    {
        if ((settings.Subscription == null || settings.Subscription == Guid.Empty) && config.Subscription != null)
            settings.Subscription = config.Subscription;

        if (settings.Output == OutputFormat.Console && config.Output.HasValue)
            settings.Output = config.Output.Value;

        if (settings.Timeframe == TimeframeType.BillingMonthToDate && config.Timeframe.HasValue)
            settings.Timeframe = config.Timeframe.Value;

        if (!settings.UseUSD && config.UseUSD.HasValue)
            settings.UseUSD = config.UseUSD.Value;

        if (settings.Metric == MetricType.ActualCost && config.Metric.HasValue)
            settings.Metric = config.Metric.Value;

        if (settings.OthersCutoff == 10 && config.OthersCutoff.HasValue)
            settings.OthersCutoff = config.OthersCutoff.Value;
    }
}

public class ConfigFileValues
{
    public Guid? Subscription { get; set; }
    public OutputFormat? Output { get; set; }
    public TimeframeType? Timeframe { get; set; }
    public bool? UseUSD { get; set; }
    public MetricType? Metric { get; set; }
    public int? OthersCutoff { get; set; }
}
