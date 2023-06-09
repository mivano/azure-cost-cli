using System.Globalization;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Rendering;
using Columns = Spectre.Console.Columns;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public class ConsoleOutputFormatter : BaseOutputFormatter
{
    public override Task WriteAccumulatedCost(AccumulatedCostSettings settings,
        AccumulatedCostDetails accumulatedCostDetails)
    {
        var todaysDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayTitle = "Today";
        var yesterdayTitle = "Yesterday";

        if (todaysDate > accumulatedCostDetails.Costs.Max(a => a.Date))
        {
            todaysDate = accumulatedCostDetails.Costs.Max(a => a.Date);
            todayTitle = todaysDate.ToString("d");
            yesterdayTitle = todaysDate.AddDays(-1).ToString("d");
        }

        var costToday = accumulatedCostDetails.Costs.Where(a => a.Date == todaysDate)
            .Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
        var costSinceStartOfCurrentMonth =
            accumulatedCostDetails.Costs.Where(x => x.Date >= todaysDate.AddDays(-todaysDate.Day + 1))
                .Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
        var costYesterday = accumulatedCostDetails.Costs.Where(a => a.Date == todaysDate.AddDays(-1))
            .Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
        var costLastSevenDays = accumulatedCostDetails.Costs.Where(x => x.Date >= todaysDate.AddDays(-7))
            .Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
        var costLastThirtyDays = accumulatedCostDetails.Costs.Where(x => x.Date >= todaysDate.AddDays(-30))
            .Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);

        var currency = settings.UseUSD ? "USD" : accumulatedCostDetails.Costs.FirstOrDefault()?.Currency;

        // Header
        var headerInfo =
            $"[bold]Azure Cost Overview[/] for [blue]{accumulatedCostDetails.Subscription.displayName}[/] from [green]{accumulatedCostDetails.Costs.Min(a => a.Date)}[/] to [green]{accumulatedCostDetails.Costs.Max(a => a.Date)}[/]";

        var rootTable = new Table();
        rootTable.Expand();
        rootTable.Title = new TableTitle(headerInfo);
        rootTable.Border(TableBorder.None);
        rootTable.ShowHeaders = false;

        rootTable.AddColumn("");

        rootTable.Columns[0].Padding(2, 2).Centered();

        // Create a table
        var table = new Table();
        //table.Title = new TableTitle("Azure Costs");
        table.Border(TableBorder.None);
        table.ShowHeaders = false;

        // Add some columns
        table.AddColumn("").Expand().Centered();
        table.AddColumn(new TableColumn("").Centered());


        // Add some rows
        table.AddRow("[green bold]" + todayTitle + ":[/]", $"{costToday:N2} {currency}");
        table.AddRow("[green bold]" + yesterdayTitle + ":[/]", $"{costYesterday:N2} {currency}");
        table.AddRow("[blue bold]Since start of " + todaysDate.ToString("MMM") + ":[/]",
            $"{costSinceStartOfCurrentMonth:N2} {currency}");
        table.AddRow("[yellow bold]Last 7 days:[/]", $"{costLastSevenDays:N2} {currency}");
        table.AddRow("[yellow bold]Last 30 days:[/]", $"{costLastThirtyDays:N2} {currency}");

        var accumulatedCostChart = new BarChart()
            .Width(60)
            .Label($"Accumulated cost in {currency}")
            .CenterLabel();

        var accumulatedCost = accumulatedCostDetails.Costs.OrderBy(x => x.Date).ToList();
        double accumulatedCostValue = 0.0;
        foreach (var day in accumulatedCost)
        {
            double costValue = settings.UseUSD ? day.CostUsd : day.Cost;
            accumulatedCostValue += costValue;
            accumulatedCostChart.AddItem(day.Date.ToString("dd MMM"), Math.Round(accumulatedCostValue, 2), Color.Green);
        }

        var forecastedData = accumulatedCostDetails
            .ForecastedCosts
            .Where(x => x.Date > accumulatedCost.Last().Date)
            .OrderBy(x => x.Date)
            .ToList();

        foreach (var day in forecastedData)
        {
            double costValue = settings.UseUSD ? day.CostUsd : day.Cost;
            accumulatedCostValue += costValue;
            accumulatedCostChart.AddItem(day.Date.ToString("dd MMM"), Math.Round(accumulatedCostValue, 2),
                Color.LightGreen);
        }

        // Render the services table
        var servicesBreakdown = new BreakdownChart()
            .UseValueFormatter(value => $"{value:N2} {currency}")
            .Expand()
            .FullSize();

        var counter = 2;
        foreach (var cost in accumulatedCostDetails.ByServiceNameCosts.TrimList(threshold: settings.OthersCutoff))
        {
            servicesBreakdown.AddItem(cost.ItemName, Math.Round(settings.UseUSD ? cost.CostUsd : cost.Cost, 2),
                Color.FromInt32(counter++));
        }

        // Render the resource groups table
        var resourceGroupBreakdown = new BreakdownChart()
            .UseValueFormatter(value => $"{value:N2} {currency}")
            .Width(60);

        counter = 2;
        foreach (var rg in accumulatedCostDetails.ByResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
        {
            resourceGroupBreakdown.AddItem(rg.ItemName, Math.Round(settings.UseUSD ? rg.CostUsd : rg.Cost, 2),
                Color.FromInt32(counter++));
        }

        // Render the locations table
        var locationsBreakdown = new BreakdownChart()
            .UseValueFormatter(value => $"{value:N2} {currency}")
            .Width(60);

        counter = 2;
        foreach (var cost in accumulatedCostDetails.ByLocationCosts.TrimList(threshold: settings.OthersCutoff))
        {
            locationsBreakdown.AddItem(cost.ItemName, Math.Round(settings.UseUSD ? cost.CostUsd : cost.Cost, 2),
                Color.FromInt32(counter++));
        }


        var subTable = new Table();
        subTable.Border(TableBorder.None);
        subTable.ShowHeaders = false;
        subTable.AddColumn("");
        subTable.AddColumn("");
        subTable.AddRow(new Rows(
                new Panel(table).Header("Azure Costs").Expand().Border(BoxBorder.Rounded),
                new Panel(servicesBreakdown).Header("By Service name").Expand().Border(BoxBorder.Rounded),
                new Panel(locationsBreakdown).Header("By Location").Expand().Border(BoxBorder.Rounded)
            )
            , new Rows(accumulatedCostChart,
                new Panel(resourceGroupBreakdown).Header("By Resource Group").Expand().Border(BoxBorder.Rounded)));

        subTable.Columns[0].Padding(2, 2).Centered();
        subTable.Columns[1].Padding(2, 2).Centered();

        rootTable.AddRow(subTable);

        AnsiConsole.Write(rootTable);


        return Task.CompletedTask;
    }

    public override Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources)
    {
        var tree = new Tree("Cost by resources");

        foreach (var resource in resources.OrderByDescending(a => a.Cost))
        {
            var table = new Table()
                .RoundedBorder()
                .AddColumn("Resource")
                .AddColumn("Resource Type")
                .AddColumn("Location")
                .AddColumn("Resource group name")
                .AddColumn("Tags")
                .AddColumn("Cost", column => column.RightAligned());

            table.AddRow(new Markup(resource.ResourceId.Split('/').Last()),
                new Markup(resource.ResourceType),
                new Markup(resource.ResourceLocation),
                new Markup(resource.ResourceGroupName),
                new Text(string.Join(",", resource.Tags)),
                settings.UseUSD
                    ? new Markup($"{resource.CostUSD:N2} USD")
                    : new Markup($"{resource.Cost:N2} {resource.Currency}"));

            var treeNode = tree.AddNode(table);

            var subTable = new Table()
                .Expand()
                .AddColumn("Service name")
                .AddColumn("Service tier")
                .AddColumn("Meter")
                .AddColumn("Cost", column => column.RightAligned());

            foreach (var metered in resources
                         .Where(a => a.ResourceId == resource.ResourceId)
                         .OrderByDescending(a => a.Cost))
            {
                subTable.AddRow(new Markup(metered.ServiceName),
                    new Markup(metered.ServiceTier),
                    new Markup(metered.Meter),
                    settings.UseUSD
                        ? new Markup($"{metered.CostUSD:N2} USD")
                        : new Markup($"{metered.Cost:N2} {metered.Currency}"));
            }

            treeNode.AddNode(subTable);
        }

        AnsiConsole.Write(tree);

        return Task.CompletedTask;
    }

    public override Task WriteBudgets(BudgetsSettings settings, IEnumerable<BudgetItem> budgets)
    {
        var table = new Table()
                .RoundedBorder()
                .Expand()
                .Title("Budgets")
                .AddColumn("Name")
                .AddColumn("Amount")
                .AddColumn("Time Grain")
                .AddColumn("Notifications")
            ;

        foreach (var budget in budgets.OrderByDescending(a => a.Name))
        {
            var notifications = new Table()
                .RoundedBorder()
                .Expand()
                .AddColumn("Name")
                .AddColumn("State")
                .AddColumn("Operator")
                .AddColumn("Threshold");


            foreach (var notification in budget.Notifications)
            {
                notifications.AddRow(new Markup(notification.Name),
                    new Markup(notification.Enabled ? ":check_mark_button: " : ":cross_mark: "),
                    new Markup(notification.Operator), new Markup(notification.Threshold.ToString("N2")));
            }

            table.AddRow(new Markup($"[bold]{budget.Name}[/]"), new Markup(budget.Amount.ToString("N2")),
                new Markup(budget.TimeGrain), notifications);
        }

        AnsiConsole.Write(table);

        return Task.CompletedTask;
    }

    public override Task WriteDailyCost(DailyCostSettings settings, IEnumerable<CostDailyItem> dailyCosts)
    {
        const string othersLabel = "(others)";
        
        var t = new Table();
        t.Border(TableBorder.None);
        t.Title($"Daily costs grouped by {settings.Dimension}");
        t.Collapse();
        t.AddColumn("").Collapse();
        t.AddColumn("").RightAligned().Collapse();
        t.AddColumn("").Expand();

// Keep track of the unique items and their assigned colors
        var colorMap = new Dictionary<string, Color>();
        var colorCounter = 0;

// List for storing unique items for the legend
        var legendData = new List<IBreakdownChartItem>();

// Get the top items across all days, according to the OthersCutoff setting
        var topItems = dailyCosts
            .GroupBy(a => a.Name)
            .Select(g => new { Name = g.Key, TotalCost = g.Sum(item => settings.UseUSD ? item.CostUsd : item.Cost) })
            .OrderByDescending(g => g.TotalCost)
            .Take(settings.OthersCutoff)
            .Select(g => g.Name)
            .ToList();

// Calculate the maximum daily cost
        var maxDailyCost = dailyCosts.GroupBy(a => a.Date)
            .Max(group =>
                group.Sum(item => topItems.Contains(item.Name) ? (settings.UseUSD ? item.CostUsd : item.Cost) : 0));

        foreach (var day in dailyCosts.GroupBy(a => a.Date).OrderBy(a => a.Key))
        {
            var data = new List<IBreakdownChartItem>();
            var dailyCost = 0D; // Keep track of the total cost for this day
            var othersCost = 0D; // Keep track of the total cost for 'Other' items

            foreach (var item in day)
            {
                var itemCost = settings.UseUSD ? item.CostUsd : item.Cost;

                if (topItems.Contains(item.Name))
                {
                    dailyCost += itemCost;

                    // If we haven't seen this item before, add a new color mapping for it
                    if (!colorMap.ContainsKey(item.Name))
                    {
                        colorMap[item.Name] = Color.FromInt32(colorCounter++);
                        legendData.Add(new BreakdownChartItem(item.Name, 0,
                            colorMap[item.Name])); // Add item to legend with initial cost of 0
                    }

                    // Add this item's cost to the data for this day, using the assigned color
                    data.Add(new BreakdownChartItem(item.Name, itemCost, colorMap[item.Name]));
                }
                else
                {
                    othersCost += itemCost;
                }
            }

            // Add 'Other' items, if there are any
            if (othersCost > 0)
            {
                dailyCost += othersCost;
                if (!colorMap.ContainsKey(othersLabel))
                {
                    colorMap[othersLabel] = Color.FromInt32(colorCounter++);
                    legendData.Add(new BreakdownChartItem(othersLabel, 0, colorMap[othersLabel]));
                }

                data.Add(new BreakdownChartItem(othersLabel, othersCost, colorMap[othersLabel]));
            }

            var c = new BreakdownBar(data);

            // Calculate width as a proportion of the maximum daily cost
            // Here we assume a maximum character width of 50, adjust this as per your requirement
            c.Width = (int)Math.Round((dailyCost / maxDailyCost) * 50);

            var cost = settings.UseUSD
                ? dailyCost.ToString("F2") + " USD"
                : dailyCost.ToString("F2") + " " + day.First().Currency;

            t.AddRow(new Markup(day.Key.ToString(CultureInfo.CurrentCulture)), new Markup("[grey42]" + cost + "[/]"), c);
        }

        t.AddEmptyRow();

        var tags = new BreakdownTags(legendData)
        {
            ShowTagValues = false
        };

        t.AddRow(new Markup("Legend"), new Markup(""), tags);

        AnsiConsole.Write(t);

       var totalSum = dailyCosts.Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
       var totalSumPanel =
           new Panel(new Text(totalSum.ToString("N2") +
                              (settings.UseUSD ? " USD" : " " + dailyCosts.First().Currency)));
       totalSumPanel.Header = new PanelHeader("Total costs", Justify.Center);
       totalSumPanel.Padding(1, 0, 1, 0);
         totalSumPanel.Border = BoxBorder.Rounded;
         
       AnsiConsole.Write(totalSumPanel);
       
        return Task.CompletedTask;
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
            return string.Format(culture, "{0} [grey]{1}[/]",
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

internal static class Ratio
{
    public static List<int> Distribute(int total, IList<int> ratios, IList<int>? minimums = null)
    {
        if (minimums != null)
        {
            ratios = ratios.Zip(minimums, (a, b) => (ratio: a, min: b)).Select(a => a.min > 0 ? a.ratio : 0).ToList();
        }

        var totalRatio = ratios.Sum();

        var totalRemaining = total;
        var distributedTotal = new List<int>();

        minimums ??= ratios.Select(_ => 0).ToList();

        foreach (var (ratio, minimum) in ratios.Zip(minimums, (a, b) => (a, b)))
        {
            var distributed = (totalRatio > 0)
                ? Math.Max(minimum, (int)Math.Ceiling(ratio * totalRemaining / (double)totalRatio))
                : totalRemaining;

            distributedTotal.Add(distributed);
            totalRatio -= ratio;
            totalRemaining -= distributed;
        }

        return distributedTotal;
    }
}