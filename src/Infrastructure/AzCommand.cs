using System.Diagnostics;
using System.Text.Json;

namespace AzureCostCli.Infrastructure;

public static class AzCommand
{
    public static string GetDefaultAzureSubscriptionId()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "az",
            Arguments = "account show",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = startInfo })
        {
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                throw new Exception($"Error executing 'az account show': {error}");
            }

            using (var jsonDocument = JsonDocument.Parse(output))
            {
                JsonElement root = jsonDocument.RootElement;
                if (root.TryGetProperty("id", out JsonElement idElement))
                {
                    string subscriptionId = idElement.GetString();
                    return subscriptionId;
                }
                else
                {
                    throw new Exception("Unable to find the 'id' property in the JSON output.");
                }
            }
        }
    }
}