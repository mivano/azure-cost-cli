using System.Globalization;
using System.Text.Json;
using AzureCostCli.Commands.AccumulatedCost;
using AzureCostCli.Commands.Budgets;
using AzureCostCli.Commands.CostByResource;
using AzureCostCli.Commands.CostByTag;
using AzureCostCli.Commands.DailyCost;
using AzureCostCli.Commands.DetectAnomaly;
using AzureCostCli.Commands.Regions;
using AzureCostCli.Commands.WhatIf;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using AzureCostCli.OutputFormatters.SpectreConsole;
using Spectre.Console;
using Spectre.Console.Json;
using Columns = Spectre.Console.Columns;

namespace AzureCostCli.OutputFormatters;

public class ConsoleOutputFormatter : BaseOutputFormatter
{
    public override Task WriteAccumulatedCost(AccumulatedCostSettings settings,
        AccumulatedCostDetails accumulatedCostDetails)
    {
        if (accumulatedCostDetails.Costs.Any()==false)
        {
            AnsiConsole.MarkupLine("[red]No data found[/]");
            return Task.CompletedTask;
        }
        
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

        var totalCostInTimeframe = accumulatedCostDetails.Costs.Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);

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
        table.Border(TableBorder.None);
        table.ShowHeaders = false;

        // Add some columns
        table.AddColumn("").Expand().Centered();
        table.AddColumn(new TableColumn("").Centered());


        // Add some rows
        table.AddRow(new Markup("[green bold]" + todayTitle + ":[/]"), new Money(costToday, currency));
        table.AddRow(new Markup("[green bold]" + yesterdayTitle + ":[/]"), new Money(costYesterday, currency));
        table.AddRow(new Markup("[blue bold]Since start of " + todaysDate.ToString("MMM") + ":[/]"),
            new Money(costSinceStartOfCurrentMonth, currency));
        table.AddRow(new Markup("[yellow bold]Last 7 days:[/]"), new Money(costLastSevenDays, currency));
        table.AddRow(new Markup("[yellow bold]Last 30 days:[/]"), new Money(costLastThirtyDays, currency));
        table.AddRow(new Markup("[yellow bold]Total in timeframe:[/]"), new Money(totalCostInTimeframe, currency));

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
        var servicesBreakdown = new BreakdownChartExt()
            .UseValueFormatter(value => Money.FormatMoney(value, currency))
            .Expand()
            .FullSize();

        var counter = 2;
        foreach (var cost in accumulatedCostDetails.ByServiceNameCosts.TrimList(threshold: settings.OthersCutoff))
        {
            servicesBreakdown.AddItem(cost.ItemName, Math.Round(settings.UseUSD ? cost.CostUsd : cost.Cost, 2),
                Color.FromInt32(counter++));
        }

        // Render the resource groups table
        BreakdownChartExt? resourceGroupBreakdown = null;
        if (settings.GetScope.IsSubscriptionBased)
        {
            resourceGroupBreakdown = new BreakdownChartExt()
                .UseValueFormatter(value => Money.FormatMoney(value, currency))
                .Width(60);

            counter = 2;
            foreach (var rg in accumulatedCostDetails.ByResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
            {
                resourceGroupBreakdown.AddItem(rg.ItemName, Math.Round(settings.UseUSD ? rg.CostUsd : rg.Cost, 2),
                    Color.FromInt32(counter++));
            }
        }

        BreakdownChartExt? subscriptionBreakdown = null;
        if (settings.GetScope.Name.Equals("EnrollmentAccount", StringComparison.InvariantCultureIgnoreCase))
        {
            // Render the resource groups table
            subscriptionBreakdown = new BreakdownChartExt()
            .UseValueFormatter(value => Money.FormatMoney(value, currency))
            .Width(60);

            counter = 2;
            foreach (var rg in accumulatedCostDetails.BySubscriptionCosts.TrimList(threshold: settings.OthersCutoff))
            {
                subscriptionBreakdown.AddItem(rg.ItemName, Math.Round(settings.UseUSD ? rg.CostUsd : rg.Cost, 2),
                    Color.FromInt32(counter++));
            }
        }

        // Render the locations table
        var locationsBreakdown = new BreakdownChartExt()
            .UseValueFormatter(value => Money.FormatMoney(value, currency))
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
        
        if (resourceGroupBreakdown!=null)
        {
            subTable.AddRow(new Rows(
                    new Panel(table).Header("Azure Costs").Expand().Border(BoxBorder.Rounded),
                    new Panel(servicesBreakdown).Header("By Service name").Expand().Border(BoxBorder.Rounded),
                    new Panel(locationsBreakdown).Header("By Location").Expand().Border(BoxBorder.Rounded),
                    new Panel(resourceGroupBreakdown).Header("By Resource Group").Expand().Border(BoxBorder.Rounded)
                )
                , new Rows(accumulatedCostChart));
        }
        else if (subscriptionBreakdown!=null)
        {
            subTable.AddRow(new Rows(
                    new Panel(table).Header("Azure Costs").Expand().Border(BoxBorder.Rounded),
                    new Panel(servicesBreakdown).Header("By Service name").Expand().Border(BoxBorder.Rounded),
                    new Panel(locationsBreakdown).Header("By Location").Expand().Border(BoxBorder.Rounded),
                    new Panel(subscriptionBreakdown).Header("By Subscription").Expand().Border(BoxBorder.Rounded)
                )
                , new Rows(accumulatedCostChart));
        }
        subTable.Columns[0].Padding(2, 2).Centered();
        subTable.Columns[1].Padding(2, 2).Centered();

        rootTable.AddRow(subTable);

        AnsiConsole.Write(rootTable);

        return Task.CompletedTask;
    }

    public override Task WriteCostByResource(CostByResourceSettings settings, IEnumerable<CostResourceItem> resources)
    {
        // When we have meter details, we output the tree, otherwise we output a table
        if (settings.ExcludeMeterDetails == false)
        {
            var tree = new Tree("Cost by resources");
            tree.Guide(TreeGuide.Line);

            foreach (var resource in resources.OrderByDescending(a => a.Cost))
            {
                var table = new Table()
                    .Border(TableBorder.SimpleHeavy)
                    .AddColumn("Resource")
                    .AddColumn("Resource Type")
                    .AddColumn("Location")
                    .AddColumn("Resource group name")
                    .AddColumn("Tags")
                    .AddColumn("Cost", column => column.RightAligned());

                table.AddRow(new Markup("[bold]" + resource.ResourceId.Split('/').Last().EscapeMarkup() + "[/]"),
                    new Markup(resource.ResourceType.EscapeMarkup()),
                    new Markup(resource.ResourceLocation.EscapeMarkup()),
                    new Markup(resource.ResourceGroupName.EscapeMarkup()),
                    resource.Tags.Any() ? new JsonText(JsonSerializer.Serialize(resource.Tags)) : new Markup(""),
                    settings.UseUSD
                        ? new Money(resource.CostUSD, "USD")
                        : new Money(resource.Cost, resource.Currency));

                var treeNode = tree.AddNode(table);


                var subTable = new Table()
                    .Expand()
                    .Border(TableBorder.Simple)
                    .AddColumn("Service name")
                    .AddColumn("Service tier")
                    .AddColumn("Meter")
                    .AddColumn("Cost", column => column.RightAligned());

                foreach (var metered in resources
                             .Where(a => a.ResourceId == resource.ResourceId)
                             .OrderByDescending(a => a.Cost))
                {
                    subTable.AddRow(new Markup(metered.ServiceName.EscapeMarkup()),
                        new Markup(metered.ServiceTier.EscapeMarkup()),
                        new Markup(metered.Meter.EscapeMarkup()),
                        settings.UseUSD
                            ? new Money(metered.CostUSD, "USD")
                            : new Money(metered.Cost, metered.Currency));
                }

                treeNode.AddNode(subTable);
            }

            AnsiConsole.Write(tree);
        }
        else
        {
            var table = new Table()
                .RoundedBorder().Expand()
                .AddColumn("Resource")
                .AddColumn("Resource Type")
                .AddColumn("Location")
                .AddColumn("Resource group name")
                .AddColumn("Tags")
                .AddColumn("Cost", column => column.Width(15).RightAligned());

            foreach (var resource in resources.OrderByDescending(a => a.Cost))
            {
                table.AddRow(new Markup("[bold]" + resource.ResourceId.Split('/').Last().EscapeMarkup() + "[/]"),
                    new Markup(resource.ResourceType.EscapeMarkup()),
                    new Markup(resource.ResourceLocation.EscapeMarkup()),
                    new Markup(resource.ResourceGroupName.EscapeMarkup()),
                    resource.Tags.Any() ? new JsonText(JsonSerializer.Serialize(resource.Tags)) : new Markup(""),
                    settings.UseUSD
                        ? new Money(resource.CostUSD, "USD")
                        : new Money(resource.Cost, resource.Currency));
            }

            AnsiConsole.Write(table);
        }

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
        
        if (dailyCosts.Any()==false)
        {
            AnsiConsole.MarkupLine("[red]No data found[/]");
            return Task.CompletedTask;
        }
        
        const string othersLabel = "(others)";

        var t = new Table();
        t.Border(TableBorder.None);
        t.Title($"Daily costs grouped by {settings.Dimension}");
        t.Collapse();
        t.AddColumn("").Collapse();
        t.AddColumn("").RightAligned().Collapse();
        t.AddColumn("").Expand();

        t.Columns[1].Width = 15;
        t.Columns[1].Alignment = Justify.Right;

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

            t.AddRow(
                new Markup(day.Key.ToString(CultureInfo.CurrentCulture)),
                new Money(dailyCost, settings.UseUSD ? "USD" : day.First().Currency, 2, null, Justify.Left),
                c);
        }

        t.AddEmptyRow();

        var tags = new BreakdownTags(legendData)
        {
            ShowTagValues = false
        };

        t.AddRow(new Markup("Legend"), new Markup(""), tags);

        AnsiConsole.Write(t);

        var costGroupedByDay = dailyCosts.GroupBy(a => a.Date).OrderBy(a => a.Key);
        // Calculate trend of the cost, so we can see if we are going up or down
        var previousCost = 0D;
        var trend = 0D;
        foreach (var day in costGroupedByDay)
        {
            var currentCost = day.Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
            if (previousCost != 0)
            {
                trend += currentCost - previousCost;
            }

            previousCost = currentCost;
        }

        var trendText = trend > 0 ? "up" : "down";
        var totalSum = dailyCosts.Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);

        var avgCost = totalSum / costGroupedByDay.Count();

        var figletTotalCost = new FigletText(totalSum.ToString("N2")).Color(trend > 0 ? Color.Red : Color.Green);
        var textTotalCost =
            new Markup(
                $"Total costs in {(settings.UseUSD ? "USD" : dailyCosts.First().Currency)}, going {trendText} {(trend > 0 ? ":chart_increasing:" : ":chart_decreasing:")}");

        var figletAvgCost = new FigletText(avgCost.ToString("N2")).Color(Color.Blue);
        var textAvgCost = new Markup("Avg daily costs in " + (settings.UseUSD ? "USD" : dailyCosts.First().Currency));


        var rule = new Rule();
        AnsiConsole.WriteLine();
        AnsiConsole.Write(rule);

        AnsiConsole.Write(new Columns(
            new Rows(figletTotalCost, textTotalCost),
            new Rows(figletAvgCost, textAvgCost)));


        return Task.CompletedTask;
    }

    public override Task WriteAnomalyDetectionResults(DetectAnomalySettings settings,
        List<AnomalyDetectionResult> anomalies)
    {
        var groupedByName = anomalies.GroupBy(a => a.Name);

        var tree = new Tree("[red]Detected Anomalies[/]");


        foreach (var item in groupedByName)
        {
            var n = tree.AddNode($"[dim]{settings.Dimension}[/]: [bold]{item.Key}[/]");

            foreach (var anomaly in item.GroupBy(a => a.AnomalyType))
            {
                foreach (var a in anomaly.OrderBy(b => b.DetectionDate))
                {
                    TreeNode subNode = null;
                    switch (anomaly.Key)
                    {
                        case AnomalyType.NewCost:
                            subNode = n.AddNode(new Markup(
                                $":money_bag: [bold]New cost detected[/] on [dim]{a.DetectionDate}[/]: [red]{a.CostDifference:N2}[/]"));
                            break;
                        case AnomalyType.RemovedCost:
                            subNode = n.AddNode(new Markup(
                                $":money_with_wings: [bold]Removed cost detected[/] on [dim]{a.DetectionDate}[/]: [red]{a.CostDifference:N2}[/]"));
                            break;
                        case AnomalyType.SteadyGrowth:
                            subNode = n.AddNode(new Markup(
                                $":chart_increasing: [bold]Steady growth in cost detected[/] on [dim]{a.DetectionDate}[/]: [red]{a.CostDifference:N2}[/]"));
                            break;
                        case AnomalyType.SignificantChange:
                            subNode = n.AddNode(new Markup(
                                $":bar_chart: [bold]Significant change in cost detected[/] on [dim]{a.DetectionDate}[/]: [red]{a.CostDifference:N2}[/]"));
                            break;
                    }

                    if (subNode != null)
                    {
                        // Only show the relevant data for the anomaly, so from now until the detection date + a couple of days
                        var relevantDays = a.Data.Where(c =>
                            c.Date >= a.DetectionDate.AddDays(-3) && c.Date <= a.DetectionDate.AddDays(3));
                        var chart = new BarChart();
                        chart.Width = 50;

                        foreach (var costData in relevantDays.OrderByDescending(c => c.Date))
                        {
                            chart.AddItem(
                                costData.Date == a.DetectionDate
                                    ? $"[bold]{costData.Date.ToString(CultureInfo.CurrentCulture)}[/]"
                                    : $"[dim]{costData.Date.ToString(CultureInfo.CurrentCulture)}[/]",
                                Math.Round(costData.Cost, 2),
                                costData.Date == a.DetectionDate ? Color.Red : Color.Green);
                        }

                        subNode.AddNode(chart);
                    }
                }
            }
        }

        AnsiConsole.Write(tree);

        return Task.CompletedTask;
    }

    public override Task WriteRegions(RegionsSettings settings, IReadOnlyCollection<AzureRegion> regions)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Region");
        table.AddColumn("Geography");
        table.AddColumn("Display Name");
        table.AddColumn("Location");
        table.AddColumn("Sustainability");
        table.AddColumn("Compliance");

        foreach (var region in regions.OrderBy(a => a.continent).ThenBy(a => a.geographyId))
        {
            table.AddRow(
                new Markup(region.continent),
                new Markup(region.geographyId),
                new Markup((region.isOpen ? "[green]" : "[red]") + region.displayName + "[/]\n[dim](" + region.id +
                           ")[/]"),
                new Markup(region.location),
                new Markup(string.Join(", ", region.sustainabilityIds)),
                new Markup(string.Join(", ", region.complianceIds.OrderBy(a => a))));
        }

        AnsiConsole.Write(table);

        return Task.CompletedTask;
    }

    public override Task WriteCostByTag(CostByTagSettings settings,
        Dictionary<string, Dictionary<string, List<CostResourceItem>>> byTags)
    {
        // When no tags are found, output no results and stop
        if (byTags.Count == 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("No resources found with one of the tags in the list: " +
                                  string.Join(',', settings.Tags));
            return Task.CompletedTask;
        }

        var tree = new Tree("[green bold]Cost by Tag[/] for [bold]" + settings.Subscription + "[/] between [bold]" +
                            settings.From + "[/] and [bold]" + settings.To + "[/]");
        AnsiConsole.WriteLine();


        foreach (var tag in byTags)
        {
            var n = tree.AddNode($"[dim]key[/]: [bold]{tag.Key}[/]");

            foreach (var tagValue in tag.Value)
            {
                var subNode = n.AddNode($"[dim]value[/]: [bold]{tagValue.Key}[/]");
                var table = new Table();
                table.Border(TableBorder.Rounded);
                table.AddColumn("Name");
                table.AddColumn("Resource Group");
                table.AddColumn("Type");
                table.AddColumn("Location");
                table.AddColumn("Cost");

                foreach (var costResourceItem in tagValue.Value.OrderByDescending(a => a.Cost))
                {
                    table.AddRow(
                        new Markup(costResourceItem.GetResourceName()),
                        new Markup(costResourceItem.ResourceGroupName),
                        new Markup(costResourceItem.ResourceType),
                        new Markup(costResourceItem.ResourceLocation),
                        settings.UseUSD
                            ? new Money(costResourceItem.CostUSD, "USD")
                            : new Money(costResourceItem.Cost, costResourceItem.Currency)
                    );
                }

                // End with a total row
                table.AddRow(
                    new Markup(""),
                    new Markup(""),
                    new Markup(""),
                    new Markup("[bold]Total[/]"),
                    settings.UseUSD
                        ? new Money(tagValue.Value.Sum(a => a.CostUSD), "USD")
                        : new Money(tagValue.Value.Sum(a => a.Cost), tagValue.Value.First().Currency)
                );

                subNode.AddNode(table);
            }
        }

        AnsiConsole.Write(tree);

        return Task.CompletedTask;
    }

    public override Task WritePricesPerRegion(WhatIfSettings settings,
        Dictionary<UsageDetails, List<PriceRecord>> pricesByRegion)
    {
        // Loop through each resource in the pricesByRegion dictionary
        // Output the name of the resource, and then a table with the prices per region
        // Highlight the current region in the table

        var tree = new Tree("[green bold]Prices per region[/] for [bold]" + settings.Subscription +
                            "[/] between [bold]" + settings.From + "[/] and [bold]" + settings.To + "[/]");


        foreach (var (resource, prices) in pricesByRegion)
        {
            var n = tree.AddNode($"[dim]Resource[/]: [bold]{resource.properties.resourceName}[/]");
            var currentPriceRegion = prices.First(a => a.Location == resource.properties.resourceLocation);
            
            n.AddNode($"[dim]Group[/]: [bold]{resource.properties.resourceGroup}[/]");
            n.AddNode($"[dim]Product[/]: [bold]{resource.properties.product}[/]");
            n.AddNode(
                $"[dim]Total quantity[/]: [bold]{resource.properties.quantity}[/] ([dim]UoM[/] {currentPriceRegion.UnitOfMeasure})");
            n.AddNode(
                $"[dim]Current cost[/]: [bold]{Money.FormatMoney(resource.properties.quantity * resource.properties.effectivePrice, resource.properties.billingCurrency)}[/]");

            var resourceTable = new Table();
            resourceTable.Border(TableBorder.Rounded);
            resourceTable.AddColumn("Region");
            resourceTable.AddColumn("Retail Price");
            resourceTable.AddColumn("Cost");
            resourceTable.AddColumn("Deviation"); // The percentage higher or lower from the current region
            resourceTable.AddColumn("1 Year Savings Plan");
            resourceTable.AddColumn("1 Year Deviation");
            resourceTable.AddColumn("3 Years Savings Plan");
            resourceTable.AddColumn("3 Years Deviation");

            foreach (var price in prices.OrderBy(a => a.RetailPrice))
            {
                // Calculate the deviation, so compared to the current region of the resource, determine how much higher or lower the price is in percentage
                // This allows us to compare the different regions to the one currently in use. 
                var deviation =
                    (price.RetailPrice - currentPriceRegion
                        .RetailPrice) / currentPriceRegion
                        .RetailPrice * 100;
                var oneYearSavingsPlan = price.SavingsPlan?.FirstOrDefault(a => a.Term == "1 Year");
                var threeYearSavingsPlan = price.SavingsPlan?.FirstOrDefault(a => a.Term == "3 Years");

                // Also add the deviations for the savings plans
                var oneYearSavingsPlanDeviation = oneYearSavingsPlan != null
                    ? (oneYearSavingsPlan.RetailPrice -
                       currentPriceRegion.RetailPrice) /
                    currentPriceRegion.RetailPrice * 100
                    : 0;
                var threeYearSavingsPlanDeviation = threeYearSavingsPlan != null
                    ? (threeYearSavingsPlan.RetailPrice -
                       currentPriceRegion.RetailPrice) /
                    currentPriceRegion.RetailPrice * 100
                    : 0;

                resourceTable.AddRow(
                    new Markup(price.Location == resource.properties.resourceLocation
                        ? $"[bold green]{price.Location}[/]"
                        : price.Location),
                    new Money(price.RetailPrice, price.CurrencyCode, 6),
                    new Money(price.RetailPrice * resource.properties.quantity, price.CurrencyCode),
                    deviation > 0 ? new Markup($"[red]{deviation:N2}%[/]") : new Markup($"[green]{deviation:N2}%[/]"),
                    oneYearSavingsPlan != null
                        ? new Money(oneYearSavingsPlan.RetailPrice, price.CurrencyCode, 6)
                        : new Markup(""),
                    oneYearSavingsPlan != null
                        ? (oneYearSavingsPlanDeviation > 0
                            ? new Markup($"[red]{oneYearSavingsPlanDeviation:N2}%[/]")
                            : new Markup($"[green]{oneYearSavingsPlanDeviation:N2}%[/]"))
                        : new Markup(""),
                    threeYearSavingsPlan != null
                        ? new Money(threeYearSavingsPlan.RetailPrice, price.CurrencyCode, 6)
                        : new Markup(""),
                    threeYearSavingsPlan != null
                        ? (threeYearSavingsPlanDeviation > 0
                            ? new Markup($"[red]{threeYearSavingsPlanDeviation:N2}%[/]")
                            : new Markup($"[green]{threeYearSavingsPlanDeviation:N2}%[/]"))
                        : new Markup("")
                );
            }

            n.AddNode(resourceTable);
        }

        AnsiConsole.Write(tree);


        return Task.CompletedTask;
    }
}