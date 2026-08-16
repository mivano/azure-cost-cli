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

    [Fact]
    public void ResolveCommandInterpreter_NeverReturnsAQuotedPath()
    {
        // ProcessStartInfo.FileName is not parsed as a command line, so a quoted ComSpec
        // would be taken as part of the file name and fail to start.
        var interpreter = AzCommand.ResolveCommandInterpreter();

        interpreter.ShouldNotBeNullOrWhiteSpace();
        interpreter.ShouldNotStartWith("\"");
        interpreter.ShouldNotEndWith("\"");
        interpreter.ShouldContain("cmd", Case.Insensitive);
    }

    [Fact]
    public async Task ObserveFaults_ObservesATaskThatFaultsAfterTheCallReturns()
    {
        // The grace-period wait can time out without throwing, so observation cannot depend
        // on it: a read that faults later must still be observed.
        var source = new TaskCompletionSource();
        var task = source.Task;

        AzCommand.ObserveFaults(task);
        source.SetException(new InvalidOperationException("pipe closed"));

        // The continuation runs synchronously on completion, so the fault is observed by now.
        await Should.ThrowAsync<InvalidOperationException>(async () => await task);
        task.Exception.ShouldNotBeNull();
    }

    [Fact]
    public void ObserveFaults_IsSafeForAlreadyCompletedAndCancelledTasks()
    {
        var completed = Task.CompletedTask;
        var faulted = Task.FromException(new InvalidOperationException("already gone"));
        var cancelled = Task.FromCanceled(new CancellationToken(canceled: true));

        Should.NotThrow(() => AzCommand.ObserveFaults(completed, faulted, cancelled));
    }

    [Fact]
    public void DescribeLauncher_WhenAzIsInvokedDirectly_AddsNothing()
    {
        AzCommand.DescribeLauncher("az").ShouldBeEmpty();
    }

    [Fact]
    public void DescribeLauncher_WhenLaunchedViaInterpreter_NamesItAsTheLauncherNotTheCli()
    {
        // The startup failure message must not read as though cmd.exe were the Azure CLI.
        var description = AzCommand.DescribeLauncher(@"C:\WINDOWS\system32\cmd.exe");

        description.ShouldContain("command interpreter");
        description.ShouldContain("cmd.exe");
        description.ShouldNotContain("Azure CLI");
    }

    [Fact]
    public void ParseSubscriptionId_WithValidJson_ReturnsId()
    {
        var id = AzCommand.ParseSubscriptionId(0, """{"id":"abc-123","name":"Sub"}""", "");

        id.ShouldBe("abc-123");
    }

    [Fact]
    public void ParseSubscriptionId_WithNonZeroExitCode_ReportsExitCodeAndStderr()
    {
        var ex = Should.Throw<Exception>(() =>
            AzCommand.ParseSubscriptionId(9009, "", "'az' is not recognized"));

        ex.Message.ShouldContain("9009");
        ex.Message.ShouldContain("'az' is not recognized");
    }

    [Fact]
    public void ParseSubscriptionId_WithNonJsonOutput_ExplainsTheOutputWasNotJson()
    {
        // What a user with 'az configure --defaults output=table' would have seen.
        var ex = Should.Throw<Exception>(() =>
            AzCommand.ParseSubscriptionId(0, "Name    CloudName    SubscriptionId", ""));

        ex.Message.ShouldContain("not valid JSON");
    }

    [Fact]
    public void ParseSubscriptionId_WithoutIdProperty_Throws()
    {
        var ex = Should.Throw<Exception>(() =>
            AzCommand.ParseSubscriptionId(0, """{"name":"Sub"}""", ""));

        ex.Message.ShouldContain("'id'");
    }

    [Theory]
    [InlineData("""{"id":""}""")]
    [InlineData("""{"id":null}""")]
    [InlineData("[]")]
    public void ParseSubscriptionId_WithUnusableId_Throws(string output)
    {
        Should.Throw<Exception>(() => AzCommand.ParseSubscriptionId(0, output, ""));
    }
}
