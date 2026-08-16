using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace AzureCostCli.Infrastructure;

public static class AzCommand
{
    private const string AzExecutable = "az";
    private const string AzArguments = "account show --output json";

    /// <summary>
    /// How long to wait for the Azure CLI to respond before giving up.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long to wait for the Azure CLI to actually terminate after being killed.
    /// </summary>
    private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Builds the start info used to invoke the Azure CLI.
    /// </summary>
    /// <remarks>
    /// On Windows the Azure CLI installed through the MSI or winget is a batch file (az.cmd);
    /// there is no az.exe. Because UseShellExecute is false, .NET calls CreateProcess directly,
    /// which only appends .exe, ignores PATHEXT and cannot execute batch files at all. Launching
    /// through the command interpreter is therefore required, even when 'az' is on the PATH.
    /// </remarks>
    internal static ProcessStartInfo BuildStartInfo() => BuildStartInfo(OperatingSystem.IsWindows());

    /// <inheritdoc cref="BuildStartInfo()"/>
    internal static ProcessStartInfo BuildStartInfo(bool isWindows)
    {
        var startInfo = isWindows
            ? new ProcessStartInfo
            {
                FileName = ResolveCommandInterpreter(),
                Arguments = $"/c {AzExecutable} {AzArguments}"
            }
            : new ProcessStartInfo
            {
                FileName = AzExecutable,
                Arguments = AzArguments
            };

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        return startInfo;
    }

    /// <summary>
    /// Resolves the Windows command interpreter, preferring an absolute path over a PATH lookup.
    /// </summary>
    internal static string ResolveCommandInterpreter()
    {
        // ComSpec is normally an unquoted absolute path, but tolerate a quoted value:
        // ProcessStartInfo.FileName is not parsed as a command line, so quotes would be
        // taken as part of the file name and the process would fail to start.
        var comSpec = Environment.GetEnvironmentVariable("ComSpec")?.Trim().Trim('"');

        if (!string.IsNullOrWhiteSpace(comSpec))
            return comSpec;

        // Fall back to the system directory rather than a bare "cmd.exe", so resolution does
        // not depend on the PATH. GetFolderPath returns an empty string off Windows, in which
        // case this degrades to the bare name.
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);

        return string.IsNullOrEmpty(systemDirectory)
            ? "cmd.exe"
            : Path.Combine(systemDirectory, "cmd.exe");
    }

    public static string GetDefaultAzureSubscriptionId()
    {
        using var process = new Process { StartInfo = BuildStartInfo() };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new Exception(
                $"Unable to start the Azure CLI ('{AzExecutable}'){DescribeLauncher(process.StartInfo.FileName)}. " +
                "Make sure the Azure CLI is installed and available on the PATH. " +
                $"({ex.Message})", ex);
        }

        // Both pipes must be drained while the process runs; reading one only after the process
        // has exited deadlocks as soon as the other pipe buffer fills up, and the Azure CLI does
        // write upgrade notices and warnings to stderr.
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
        {
            KillAndObserve(process, outputTask, errorTask);

            throw new Exception(
                $"Timed out after {Timeout.TotalSeconds:N0} seconds waiting for " +
                $"'{AzExecutable} {AzArguments}'.");
        }

        return ParseSubscriptionId(
            process.ExitCode,
            outputTask.GetAwaiter().GetResult(),
            errorTask.GetAwaiter().GetResult());
    }

    /// <summary>
    /// Interprets the outcome of the Azure CLI invocation and extracts the subscription ID.
    /// </summary>
    internal static string ParseSubscriptionId(int exitCode, string output, string error)
    {
        if (exitCode != 0)
        {
            throw new Exception(
                $"Error executing '{AzExecutable} {AzArguments}' (exit code {exitCode}): {error.Trim()}");
        }

        using var jsonDocument = ParseJson(output);

        if (jsonDocument.RootElement.ValueKind == JsonValueKind.Object &&
            jsonDocument.RootElement.TryGetProperty("id", out var idElement) &&
            idElement.GetString() is { Length: > 0 } subscriptionId)
        {
            return subscriptionId;
        }

        throw new Exception("Unable to find the 'id' property in the JSON output.");
    }

    /// <summary>
    /// Describes the launcher when the Azure CLI is not invoked directly, so that diagnostics
    /// do not read as if the command interpreter itself were the Azure CLI.
    /// </summary>
    internal static string DescribeLauncher(string fileName) =>
        string.Equals(fileName, AzExecutable, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : $" via the command interpreter ('{fileName}')";

    /// <summary>
    /// Ensures a fault on any of the given tasks is observed, whenever it happens.
    /// </summary>
    /// <remarks>
    /// Waiting on the tasks is not sufficient on its own: a bounded wait can time out without
    /// throwing, leaving a task to fault afterwards with nobody watching. Accessing Exception
    /// from a faulted continuation observes it even after the caller has moved on.
    /// </remarks>
    internal static void ObserveFaults(params Task[] tasks)
    {
        foreach (var task in tasks)
        {
            _ = task.ContinueWith(
                static faulted => _ = faulted.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    /// <summary>
    /// Terminates a process that overran its timeout and observes the in-flight pipe reads,
    /// so neither the process nor the read tasks outlive this call unnoticed.
    /// </summary>
    private static void KillAndObserve(Process process, params Task[] readTasks)
    {
        ObserveFaults(readTasks);

        try
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit((int)KillTimeout.TotalMilliseconds);
        }
        catch
        {
            // Nothing useful to do if the process cannot be killed; the original timeout
            // is the error worth surfacing.
        }

        try
        {
            // Give the reads a grace period to drain against the now-closed pipes. A timeout
            // here is fine: the continuations above still observe anything that faults later.
            Task.WaitAll(readTasks, KillTimeout);
        }
        catch
        {
            // Faulted reads are expected once the process has been killed.
        }
    }

    private static JsonDocument ParseJson(string output)
    {
        try
        {
            return JsonDocument.Parse(output);
        }
        catch (JsonException ex)
        {
            // Most likely an 'output' default configured through 'az configure' or AZURE_CORE_OUTPUT,
            // which the explicit --output json should already override, but be explicit about it.
            throw new Exception(
                $"The output of '{AzExecutable} {AzArguments}' is not valid JSON: {ex.Message}", ex);
        }
    }
}
