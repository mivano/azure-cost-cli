using System.Globalization;
using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using Spectre.Console;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public class ConsoleOutputFormatter : BaseOutputFormatter
{
    public override Task WriteAccumulatedCost(AccumulatedCostSettings settings, AccumulatedCostDetails accumulatedCostDetails)
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
        
        var costToday = accumulatedCostDetails.Costs.Where(a => a.Date == todaysDate).Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
        var costSinceStartOfCurrentMonth =
            accumulatedCostDetails.Costs.Where(x => x.Date >= todaysDate.AddDays(-todaysDate.Day + 1)).Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
        var costYesterday = accumulatedCostDetails.Costs.Where(a => a.Date == todaysDate.AddDays(-1)).Sum(a=>settings.UseUSD ? a.CostUsd : a.Cost);
        var costLastSevenDays = accumulatedCostDetails.Costs.Where(x => x.Date >= todaysDate.AddDays(-7)).Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);
        var costLastThirtyDays = accumulatedCostDetails.Costs.Where(x => x.Date >= todaysDate.AddDays(-30)).Sum(a => settings.UseUSD ? a.CostUsd : a.Cost);

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
        table.AddRow("[green bold]" +todayTitle + ":[/]", $"{costToday:N2} {currency}");
        table.AddRow("[green bold]" + yesterdayTitle + ":[/]", $"{costYesterday:N2} {currency}");
        table.AddRow("[blue bold]Since start of " + todaysDate.ToString("MMM")+ ":[/]", $"{costSinceStartOfCurrentMonth:N2} {currency}");
        table.AddRow("[yellow bold]Last 7 days:[/]", $"{costLastSevenDays:N2} {currency}");
        table.AddRow("[yellow bold]Last 30 days:[/]", $"{costLastThirtyDays:N2} {currency}");

        var accumulatedCostChart = new BarChart()
            .Width(60)
            .Label("Accumulated cost")
            .CenterLabel();

        var accumulatedCost = accumulatedCostDetails.Costs.OrderBy(x => x.Date).ToList();
        double accumulatedCostValue = 0.0;
        foreach (var day in accumulatedCost)
        {
            double costValue = settings.UseUSD ? day.CostUsd : day.Cost;
            accumulatedCostValue += costValue;
            accumulatedCostChart.AddItem(day.Date.ToString("dd MMM"), Math.Round(accumulatedCostValue, 2), Color.Green);
        }

        var forecastedData = accumulatedCostDetails.ForecastedCosts.Where(x => x.Date > accumulatedCost.Last().Date).OrderBy(x => x.Date)
        .ToList();
      
        foreach (var day in forecastedData)
        {
            double costValue = settings.UseUSD ? day.CostUsd : day.Cost;
            accumulatedCostValue += costValue;
            accumulatedCostChart.AddItem(day.Date.ToString("dd MMM"), Math.Round(accumulatedCostValue, 2), Color.LightGreen);
        }

        // Render the services table
        var servicesBreakdown = new BreakdownChart()
                .Expand()
                .FullSize()
            ;

        var counter = 2;
        foreach (var cost in accumulatedCostDetails.ByServiceNameCosts.TrimList(threshold: settings.OthersCutoff))
        {
            servicesBreakdown.AddItem(cost.ItemName, Math.Round(settings.UseUSD ? cost.CostUsd : cost.Cost, 2), Color.FromInt32(counter++));
        }

        // Render the resource groups table
        var resourceGroupBreakdown = new BreakdownChart()
            .Width(60);

        counter = 2;
        foreach (var rg in accumulatedCostDetails.ByResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
        {
            resourceGroupBreakdown.AddItem(rg.ItemName, Math.Round(settings.UseUSD ? rg.CostUsd : rg.Cost, 2), Color.FromInt32(counter++));
        }

        // Render the locations table
        var locationsBreakdown = new BreakdownChart()
            .Width(60);

        counter = 2;
        foreach (var cost in accumulatedCostDetails.ByLocationCosts.TrimList(threshold: settings.OthersCutoff))
        {
            locationsBreakdown.AddItem(cost.ItemName, Math.Round(settings.UseUSD ? cost.CostUsd : cost.Cost, 2), Color.FromInt32(counter++));
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
            
        foreach (var resource in resources.OrderByDescending(a=>a.Cost))
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
               new Text(string.Join(",",resource.Tags)),
               settings.UseUSD ? new Markup($"{resource.CostUSD:N2} USD") : new Markup($"{resource.Cost:N2} {resource.Currency}"));

           var treeNode = tree.AddNode(table);
           
           var subTable = new Table()
               .Expand()
               .AddColumn("Service name")
               .AddColumn("Service tier")
               .AddColumn("Meter")
               .AddColumn("Cost",column => column.RightAligned());

           foreach (var metered in resources
                        .Where(a=>a.ResourceId == resource.ResourceId)
                        .OrderByDescending(a=>a.Cost))
           {
               subTable.AddRow(new Markup(metered.ServiceName),
                   new Markup(metered.ServiceTier),
                   new Markup(metered.Meter),
                   settings.UseUSD ? new Markup($"{metered.CostUSD:N2} USD"):new Markup($"{metered.Cost:N2} {metered.Currency}"));
           }
           
           treeNode.AddNode(subTable);
        }
      
        AnsiConsole.Write(tree);
        
        return Task.CompletedTask;
    }
}