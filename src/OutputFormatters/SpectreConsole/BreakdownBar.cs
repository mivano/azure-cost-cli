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
internal sealed class BreakdownBar : Renderable
{
    private readonly List<IBreakdownChartItem> _data;

    public int? Width { get; set; }

    public BreakdownBar(List<IBreakdownChartItem> data)
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
        var width = Math.Min(Width ?? maxWidth, maxWidth);

        // Chart
        var maxValue = _data.Sum(i => i.Value);
        var items = _data.ToArray();
        var bars = Ratio.Distribute(width,
            items.Select(i => Math.Max(0, (int)(width * (i.Value / maxValue)))).ToArray());

        for (var index = 0; index < items.Length; index++)
        {
            yield return new Segment(new string('█', bars[index]), new Style(items[index].Color));
        }

        yield return Segment.LineBreak;
    }
}