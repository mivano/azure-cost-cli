using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace AzureCostCli.OutputFormatters.SpectreConsole;

/*
Some of the SpectreConsole code is internal, so copied here for reuse.
 
The following license applies to this code:

MIT License

Copyright (c) 2020 Patrik Svensson, Phil Scott, Nils Andresen

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
internal sealed class BreakdownTags : Renderable
{
    private readonly List<IBreakdownChartItem> _data;

    public int? Width { get; set; }
    public CultureInfo? Culture { get; set; }
    public bool ShowTagValues { get; set; } = true;
    public Func<double, CultureInfo, string>? ValueFormatter { get; set; }

    public BreakdownTags(List<IBreakdownChartItem> data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    protected override Measurement Measure(RenderOptions options, int maxWidth)
    {
        var width = Math.Min(Width ?? maxWidth, maxWidth);
        return new Measurement(width, width);
    }

    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var culture = Culture ?? CultureInfo.InvariantCulture;

        var panels = new List<Panel>();
        foreach (var item in _data)
        {
            var panel = new Panel(GetTag(item, culture));
            //panel.Inline = true;
            panel.Padding = new Padding(0, 0, 2, 0);
            panel.NoBorder();

            panels.Add(panel);
        }

        foreach (var segment in ((IRenderable)new Columns(panels).Padding(0, 0)).Render(options, maxWidth))
        {
            yield return segment;
        }
    }

    private string GetTag(IBreakdownChartItem item, CultureInfo culture)
    {
        return string.Format(
            culture, "[{0}]■[/] {1}",
            item.Color.ToMarkup() ?? "default",
            FormatValue(item, culture)).Trim();
    }

    private string FormatValue(IBreakdownChartItem item, CultureInfo culture)
    {
        var formatter = ValueFormatter ?? DefaultFormatter;

        if (ShowTagValues)
        {
            return string.Format(culture, "{0} {1}",
                item.Label.EscapeMarkup(),
                formatter(item.Value, culture));
        }

        return item.Label.EscapeMarkup();
    }

    private static string DefaultFormatter(double value, CultureInfo culture)
    {
        return value.ToString(culture);
    }
}