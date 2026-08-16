using AzureCostCli.Infrastructure;
using Shouldly;

namespace AzureCostCli.Tests.Infrastructure;

public class AzCommandTests
{
    [Fact]
    public void BuildStartInfo_AlwaysRequestsJsonOutput()
    {
        // A user with 'az configure --defaults output=table' (or AZURE_CORE_OUTPUT=table)
        // would otherwise get non-JSON back, which fails to parse.
        var startInfo = AzCommand.BuildStartInfo();

        startInfo.Arguments.ShouldContain("account show");
        startInfo.Arguments.ShouldContain("--output json");
    }

    [Fact]
    public void BuildStartInfo_RedirectsBothStreamsWithoutShellExecute()
    {
        var startInfo = AzCommand.BuildStartInfo();

        startInfo.RedirectStandardOutput.ShouldBeTrue();
        startInfo.RedirectStandardError.ShouldBeTrue();
        startInfo.UseShellExecute.ShouldBeFalse();
        startInfo.CreateNoWindow.ShouldBeTrue();
    }

    [Fact]
    public void BuildStartInfo_OnWindows_LaunchesThroughCommandInterpreter()
    {
        // The MSI/winget Azure CLI is az.cmd; CreateProcess cannot execute batch files,
        // so it has to go through the command interpreter. See issue #347.
        var startInfo = AzCommand.BuildStartInfo(isWindows: true);

        startInfo.FileName.ShouldContain("cmd", Case.Insensitive);
        startInfo.Arguments.ShouldBe("/c az account show --output json");
    }

    [Fact]
    public void BuildStartInfo_OnNonWindows_InvokesAzDirectly()
    {
        var startInfo = AzCommand.BuildStartInfo(isWindows: false);

        startInfo.FileName.ShouldBe("az");
        startInfo.Arguments.ShouldBe("account show --output json");
    }

    [Fact]
    public void BuildStartInfo_DefaultsToTheCurrentOperatingSystem()
    {
        var expected = AzCommand.BuildStartInfo(OperatingSystem.IsWindows());
        var actual = AzCommand.BuildStartInfo();

        actual.FileName.ShouldBe(expected.FileName);
        actual.Arguments.ShouldBe(expected.Arguments);
    }
}
