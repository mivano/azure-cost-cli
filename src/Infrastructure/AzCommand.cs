using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace AzureCostCli.Infrastructure;

public static class AzCommand
{
    private const string AzArguments = "account show --output json";

    /// <summary>
    /// How long to wait for the Azure CLI to respond before giving up.
    /// </summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

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
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                Arguments = $"/c az {AzArguments}"
            }
            : new ProcessStartInfo
            {
                FileName = "az",
                Arguments = AzArguments
            };

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        return startInfo;
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
                $"Unable to start the Azure CLI ('{process.StartInfo.FileName}'). " +
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
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Nothing useful to do if the process cannot be killed.
            }

            throw new Exception(
                $"Timed out after {Timeout.TotalSeconds:N0} seconds waiting for 'az {AzArguments}'.");
        }

        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new Exception(
                $"Error executing 'az {AzArguments}' (exit code {process.ExitCode}): {error.Trim()}");
        }

        using var jsonDocument = ParseJson(output);

        if (jsonDocument.RootElement.TryGetProperty("id", out var idElement) &&
            idElement.GetString() is { Length: > 0 } subscriptionId)
        {
            return subscriptionId;
        }

        throw new Exception("Unable to find the 'id' property in the JSON output.");
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
                $"The output of 'az {AzArguments}' is not valid JSON: {ex.Message}", ex);
        }
    }
}
