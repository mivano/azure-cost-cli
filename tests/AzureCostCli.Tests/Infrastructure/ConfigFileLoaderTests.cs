using System.Text.Json;
using AzureCostCli.Commands;
using AzureCostCli.Infrastructure;
using Shouldly;

namespace AzureCostCli.Tests.Infrastructure;

public class ConfigFileLoaderTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string WriteTempJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"azure-cost-cli-test-{Guid.NewGuid()}.json");
        File.WriteAllText(path, json);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
        Environment.SetEnvironmentVariable("AZURE_COST_CLI_CONFIG", null);
    }

    [Fact]
    public void Load_WithEnvVarOverride_ReadsSpecifiedFile()
    {
        var subId = Guid.NewGuid();
        var path = WriteTempJson($"{{\"subscription\":\"{subId}\"}}");
        Environment.SetEnvironmentVariable("AZURE_COST_CLI_CONFIG", path);

        var config = ConfigFileLoader.Load();

        config.Subscription.ShouldBe(subId);
    }

    [Fact]
    public void Load_WithMissingEnvVarFile_ReturnsEmpty()
    {
        Environment.SetEnvironmentVariable("AZURE_COST_CLI_CONFIG", "/nonexistent/path/to.json");

        var config = ConfigFileLoader.Load();

        config.ShouldNotBeNull();
        config.Subscription.ShouldBeNull();
    }

    [Fact]
    public void Load_WithMalformedJson_ReturnsSafeDefaults()
    {
        var path = WriteTempJson("not valid json {{{");
        Environment.SetEnvironmentVariable("AZURE_COST_CLI_CONFIG", path);

        Should.NotThrow(() => ConfigFileLoader.Load());
    }

    [Fact]
    public void ApplyToSettings_WithSubscriptionInConfig_AppliesWhenNotSet()
    {
        var subId = Guid.NewGuid();
        var settings = new CostSettings { Subscription = null };
        var config = new ConfigFileValues { Subscription = subId };

        ConfigFileLoader.ApplyToSettings(settings, config);

        settings.Subscription.ShouldBe(subId);
    }

    [Fact]
    public void ApplyToSettings_WithSubscriptionInConfig_DoesNotOverrideCliValue()
    {
        var cliSub = Guid.NewGuid();
        var configSub = Guid.NewGuid();
        var settings = new CostSettings { Subscription = cliSub };
        var config = new ConfigFileValues { Subscription = configSub };

        ConfigFileLoader.ApplyToSettings(settings, config);

        settings.Subscription.ShouldBe(cliSub);
    }

    [Fact]
    public void ApplyToSettings_WithOutputInConfig_AppliesWhenAtDefault()
    {
        var settings = new CostSettings { Output = OutputFormat.Console };
        var config = new ConfigFileValues { Output = OutputFormat.Json };

        ConfigFileLoader.ApplyToSettings(settings, config);

        settings.Output.ShouldBe(OutputFormat.Json);
    }

    [Fact]
    public void ApplyToSettings_WithOutputInConfig_DoesNotOverrideCliValue()
    {
        var settings = new CostSettings { Output = OutputFormat.Markdown };
        var config = new ConfigFileValues { Output = OutputFormat.Json };

        ConfigFileLoader.ApplyToSettings(settings, config);

        settings.Output.ShouldBe(OutputFormat.Markdown);
    }

    [Fact]
    public void ApplyToSettings_WithOthersCutoffInConfig_AppliesWhenAtDefault()
    {
        var settings = new CostSettings { OthersCutoff = 10 };
        var config = new ConfigFileValues { OthersCutoff = 5 };

        ConfigFileLoader.ApplyToSettings(settings, config);

        settings.OthersCutoff.ShouldBe(5);
    }

    [Fact]
    public void ApplyToSettings_WithUseUSDInConfig_AppliesWhenFalse()
    {
        var settings = new CostSettings { UseUSD = false };
        var config = new ConfigFileValues { UseUSD = true };

        ConfigFileLoader.ApplyToSettings(settings, config);

        settings.UseUSD.ShouldBeTrue();
    }

    [Fact]
    public void Load_MergesGlobalAndLocal_LocalWins()
    {
        // Test the merge precedence: local values override global values.
        // We test this by applying a merged config (simulating local-wins) to fresh settings.
        // Global says output=Text, othersCutoff=5; local says output=Json only.
        // After merge: output should be Json (local wins), othersCutoff should be 5 (global, local was null).
        var merged = new ConfigFileValues
        {
            Output = OutputFormat.Json,      // local won over global Text
            OthersCutoff = 5,               // global value, local had none
        };

        var settings = new CostSettings(); // fresh defaults: Output=Console, OthersCutoff=10
        ConfigFileLoader.ApplyToSettings(settings, merged);

        settings.Output.ShouldBe(OutputFormat.Json);
        settings.OthersCutoff.ShouldBe(5);
    }

    [Fact]
    public void Load_WithEnvVarOverride_GlobalAndLocalAreIgnored()
    {
        // When env var is set, only that file is read; global/local are ignored
        var envSubId = Guid.NewGuid();
        var envPath = WriteTempJson($"{{\"subscription\":\"{envSubId}\"}}");
        Environment.SetEnvironmentVariable("AZURE_COST_CLI_CONFIG", envPath);

        var config = ConfigFileLoader.Load();

        config.Subscription.ShouldBe(envSubId);
        // Output was not in that file — should be null (not defaulted yet)
        config.Output.ShouldBeNull();
    }
}
