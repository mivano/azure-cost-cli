using AzureCostCli.Commands.DetectAnomaly;
using AzureCostCli.OutputFormatters;
using Shouldly;
// AnomalyType is defined in the DetectAnomaly namespace
using AnomalyType = AzureCostCli.Commands.DetectAnomaly.AnomalyType;

namespace AzureCostCli.Tests.OutputFormatters;

[Collection("ConsoleOutputTests")]
public class AnomalyFormatterTests
{
    private readonly TextOutputFormatter _textFormatter = new();
    private readonly MarkdownOutputFormatter _markdownFormatter = new();

    private static DetectAnomalySettings DefaultSettings() => new DetectAnomalySettings();

    [Fact]
    public async Task TextFormatter_WriteAnomalyDetectionResults_EmptyList_WritesNoAnomaliesMessage()
    {
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await _textFormatter.WriteAnomalyDetectionResults(DefaultSettings(), new List<AnomalyDetectionResult>());
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().ShouldContain("No anomalies detected.");
    }

    [Fact]
    public async Task MarkdownFormatter_WriteAnomalyDetectionResults_EmptyList_WritesNoAnomaliesMessage()
    {
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            await _markdownFormatter.WriteAnomalyDetectionResults(DefaultSettings(), new List<AnomalyDetectionResult>());
        }
        finally
        {
            Console.SetOut(original);
        }

        output.ToString().ShouldContain("No anomalies detected.");
    }

    [Fact]
    public async Task TextFormatter_WriteAnomalyDetectionResults_WithItems_DoesNotWriteNoAnomaliesMessage()
    {
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            var anomalies = new List<AnomalyDetectionResult>
            {
                new AnomalyDetectionResult { Name = "ServiceA", AnomalyType = AnomalyType.SignificantChange, Message = "Cost spike detected" }
            };
            await _textFormatter.WriteAnomalyDetectionResults(DefaultSettings(), anomalies);
        }
        finally
        {
            Console.SetOut(original);
        }

        var text = output.ToString();
        text.ShouldNotContain("No anomalies detected.");
        text.ShouldContain("ServiceA");
    }

    [Fact]
    public async Task MarkdownFormatter_WriteAnomalyDetectionResults_WithItems_DoesNotWriteNoAnomaliesMessage()
    {
        var output = new StringWriter();
        var original = Console.Out;
        Console.SetOut(output);
        try
        {
            var anomalies = new List<AnomalyDetectionResult>
            {
                new AnomalyDetectionResult { Name = "ServiceA", AnomalyType = AnomalyType.SignificantChange, Message = "Cost spike detected" }
            };
            await _markdownFormatter.WriteAnomalyDetectionResults(DefaultSettings(), anomalies);
        }
        finally
        {
            Console.SetOut(original);
        }

        var text = output.ToString();
        text.ShouldNotContain("No anomalies detected.");
        text.ShouldContain("ServiceA");
    }
}
