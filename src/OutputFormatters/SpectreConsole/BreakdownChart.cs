using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace AzureCostCli.OutputFormatters.SpectreConsole;

public static class BreakdownChartExtExtensions
{
    /// <summary>
    /// Adds an item to the breakdown chart.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <param name="label">The item label.</param>
    /// <param name="value">The item value.</param>
    /// <param name="color">The item color.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt AddItem(this BreakdownChartExt chart, string label, double value, Color color)
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        chart.Data.Add(new BreakdownChartItem(label, value, color));
        return chart;
    }

    /// <summary>
    /// Adds an item to the breakdown chart.
    /// </summary>
    /// <typeparam name="T">A type that implements <see cref="IBreakdownChartExtItem"/>.</typeparam>
    /// <param name="chart">The breakdown chart.</param>
    /// <param name="item">The item.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt AddItem<T>(this BreakdownChartExt chart, T item)
        where T : IBreakdownChartItem
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        if (item is BreakdownChartItem chartItem)
        {
            chart.Data.Add(chartItem);
        }
        else
        {
            chart.Data.Add(
                new BreakdownChartItem(
                    item.Label,
                    item.Value,
                    item.Color));
        }

        return chart;
    }

    /// <summary>
    /// Adds multiple items to the breakdown chart.
    /// </summary>
    /// <typeparam name="T">A type that implements <see cref="IBreakdownChartExtItem"/>.</typeparam>
    /// <param name="chart">The breakdown chart.</param>
    /// <param name="items">The items.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt AddItems<T>(this BreakdownChartExt chart, IEnumerable<T> items)
        where T : IBreakdownChartItem
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        foreach (var item in items)
        {
            AddItem(chart, item);
        }

        return chart;
    }

    /// <summary>
    /// Adds multiple items to the breakdown chart.
    /// </summary>
    /// <typeparam name="T">A type that implements <see cref="IBarChartItem"/>.</typeparam>
    /// <param name="chart">The breakdown chart.</param>
    /// <param name="items">The items.</param>
    /// <param name="converter">The converter that converts instances of <c>T</c> to <see cref="IBreakdownChartExtItem"/>.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt AddItems<T>(this BreakdownChartExt chart, IEnumerable<T> items, Func<T, IBreakdownChartItem> converter)
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (converter is null)
        {
            throw new ArgumentNullException(nameof(converter));
        }

        foreach (var item in items)
        {
            chart.Data.Add(converter(item));
        }

        return chart;
    }

    /// <summary>
    /// Sets the width of the breakdown chart.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <param name="width">The breakdown chart width.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt Width(this BreakdownChartExt chart, int? width)
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        chart.Width = width;
        return chart;
    }

    /// <summary>
    /// Tags will be shown.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <param name="func">The value formatter to use.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt UseValueFormatter(this BreakdownChartExt chart, Func<double, CultureInfo, string>? func)
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        chart.ValueFormatter = func;
        return chart;
    }

    /// <summary>
    /// Tags will be shown.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <param name="func">The value formatter to use.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt UseValueFormatter(this BreakdownChartExt chart, Func<double, string>? func)
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        chart.ValueFormatter = func != null
            ? (value, _) => func(value)
            : null;

        return chart;
    }

    /// <summary>
    /// Tags will be shown.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt ShowPercentage(this BreakdownChartExt chart)
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        chart.ValueFormatter = (value, culture) => string.Format(culture, "{0}%", value);

        return chart;
    }

    /// <summary>
    /// Tags will be shown.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt ShowTags(this BreakdownChartExt chart)
    {
        return ShowTags(chart, true);
    }

    /// <summary>
    /// Tags will be not be shown.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt HideTags(this BreakdownChartExt chart)
    {
        return ShowTags(chart, false);
    }

    /// <summary>
    /// Sets whether or not tags will be shown.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <param name="show">Whether or not tags will be shown.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt ShowTags(this BreakdownChartExt chart, bool show)
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        chart.ShowTags = show;
        return chart;
    }

    /// <summary>
    /// Tag values will be shown.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt ShowTagValues(this BreakdownChartExt chart)
    {
        return ShowTagValues(chart, true);
    }

    /// <summary>
    /// Tag values will be not be shown.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt HideTagValues(this BreakdownChartExt chart)
    {
        return ShowTagValues(chart, false);
    }

    /// <summary>
    /// Sets whether or not tag values will be shown.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <param name="show">Whether or not tag values will be shown.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt ShowTagValues(this BreakdownChartExt chart, bool show)
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        chart.ShowTagValues = show;
        return chart;
    }

    /// <summary>
    /// Chart and tags is rendered in compact mode.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt Compact(this BreakdownChartExt chart)
    {
        return Compact(chart, true);
    }

    /// <summary>
    /// Chart and tags is rendered in full size mode.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt FullSize(this BreakdownChartExt chart)
    {
        return Compact(chart, false);
    }

    /// <summary>
    /// Sets whether or not the chart and tags should be rendered in compact mode.
    /// </summary>
    /// <param name="chart">The breakdown chart.</param>
    /// <param name="compact">Whether or not the chart and tags should be rendered in compact mode.</param>
    /// <returns>The same instance so that multiple calls can be chained.</returns>
    public static BreakdownChartExt Compact(this BreakdownChartExt chart, bool compact)
    {
        if (chart is null)
        {
            throw new ArgumentNullException(nameof(chart));
        }

        chart.Compact = compact;
        return chart;
    }
}

/*
Some of the SpectreConsole code is internal, so copied here for reuse.
 
The following license applies to this code:

MIT License

Copyright (c) 2020 Patrik Svensson, Phil Scott, Nils Andresen

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
public  class BreakdownChartExt : Renderable, IHasCulture, IExpandable
{
    /// <summary>
    /// Gets the breakdown chart data.
    /// </summary>
    public List<IBreakdownChartItem> Data { get; }

    /// <summary>
    /// Gets or sets the width of the breakdown chart.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not to show tags.
    /// </summary>
    public bool ShowTags { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether or not to show tag values.
    /// </summary>
    public bool ShowTagValues { get; set; } = true;

    /// <summary>
    /// Gets or sets the tag value formatter.
    /// </summary>
    public Func<double, CultureInfo, string>? ValueFormatter { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not the
    /// chart and tags should be rendered in compact mode.
    /// </summary>
    public bool Compact { get; set; } = true;

    /// <summary>
    /// Gets or sets the <see cref="CultureInfo"/> to use
    /// when rendering values.
    /// </summary>
    /// <remarks>Defaults to invariant culture.</remarks>
    public CultureInfo? Culture { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not the object should
    /// expand to the available space. If <c>false</c>, the object's
    /// width will be auto calculated.
    /// </summary>
    public bool Expand { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="BreakdownChartExt"/> class.
    /// </summary>
    public BreakdownChartExt()
    {
        Data = new List<IBreakdownChartItem>();
        Culture = CultureInfo.InvariantCulture;
    }

    /// <inheritdoc/>
    protected override Measurement Measure(RenderOptions options, int maxWidth)
    {
        var width = Math.Min(Width ?? maxWidth, maxWidth);
        return new Measurement(width, width);
    }

    /// <inheritdoc/>
    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        var width = Math.Min(Width ?? maxWidth, maxWidth);

        var grid = new Grid().Width(width);
        grid.AddColumn(new GridColumn().NoWrap());

        // Bar
        grid.AddRow(new BreakdownBar(Data)
        {
            Width = width,
        });

        if (ShowTags)
        {
            if (!Compact)
            {
                grid.AddEmptyRow();
            }

            // Tags
            grid.AddRow(new BreakdownTags(Data)
            {
                Width = width,
                Culture = Culture,
                ShowTagValues = ShowTagValues,
                ValueFormatter = ValueFormatter,
            });
        }

        if (!Expand)
        {
            grid.Collapse();
        }

        return ((IRenderable)grid).Render(options, width);
    }
}