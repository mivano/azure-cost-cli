using AzureCostCli.CostApi;
using AzureCostCli.Infrastructure;
using Spectre.Console;

namespace AzureCostCli.Commands.ShowCommand.OutputFormatters;

public class ConsoleOutputFormatter : OutputFormatter
{
    public override Task WriteOutput(ShowSettings settings, IEnumerable<CostItem> costs,
        IEnumerable<CostItem> forecastedCosts,
        IEnumerable<CostNamedItem> byServiceNameCosts,
        IEnumerable<CostNamedItem> byLocationCosts,
        IEnumerable<CostNamedItem> byResourceGroupCosts)
    {
        var todaysDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayTitle = "Today";
        var yesterdayTitle = "Yesterday";
        
        if (todaysDate > costs.Max(a => a.Date))
        {
            todaysDate = costs.Max(a => a.Date);
            todayTitle = todaysDate.ToString("d");
            yesterdayTitle = todaysDate.AddDays(-1).ToString("d");
        }
        
        var costToday = costs.Where(a => a.Date == todaysDate).Sum(a => a.Cost);
        var costSinceStartOfCurrentMonth =
            costs.Where(x => x.Date >= todaysDate.AddDays(-todaysDate.Day + 1)).Sum(x => x.Cost);
        var costYesterday = costs.Where(a => a.Date == todaysDate.AddDays(-1)).Sum(a=>a.Cost);
        var costLastSevenDays = costs.Where(x => x.Date >= todaysDate.AddDays(-7)).Sum(x => x.Cost);
        var costLastThirtyDays = costs.Where(x => x.Date >= todaysDate.AddDays(-30)).Sum(x => x.Cost);

        var currency = costs.FirstOrDefault()?.Currency;

        // Header
        var headerInfo =
            $"Azure Cost Overview for [blue]{settings.Subscription}[/] from [green]{costs.Min(a => a.Date)}[/] to [green]{costs.Max(a => a.Date)}[/]";

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

        // Get the last 7 days of costs, starting by the current date, and iterate over them
        // to see if there are any cost spikes

        var last7DaysChart = new BarChart()
            .Width(60)
            .Label("Last 7 days")
            .CenterLabel();

        var lastSevenDays = costs.Where(x => x.Date >= todaysDate.AddDays(-7)).OrderBy(x => x.Date).ToList();
        foreach (var day in lastSevenDays)
        {
            last7DaysChart.AddItem(day.Date.ToString("dd MMM"), Math.Round(day.Cost, 2), Color.Green);
        }


        var nextSevenDaysOfForecast = forecastedCosts.Where(x => x.Date >= todaysDate.AddDays(1)).OrderBy(x => x.Date)
            .Take(14).ToList();
        var next7DaysChart = new BarChart()
            .Width(60)
            .Label($"Next {nextSevenDaysOfForecast.Count} days")
            .CenterLabel();
        foreach (var day in nextSevenDaysOfForecast)
        {
            next7DaysChart.AddItem(day.Date.ToString("dd MMM"), Math.Round(day.Cost, 2), Color.Olive);
        }

        // Render the services table
        var servicesBreakdown = new BreakdownChart()
                .Expand()
                .FullSize()
            ;

        var counter = 2;
        foreach (var cost in byServiceNameCosts.TrimList(threshold: settings.OthersCutoff))
        {
            servicesBreakdown.AddItem(cost.ItemName, Math.Round(cost.Cost, 2), Color.FromInt32(counter++));
        }

        // Render the resource groups table
        var resourceGroupBreakdown = new BreakdownChart()
            .Width(60);

        counter = 2;
        foreach (var rg in byResourceGroupCosts.TrimList(threshold: settings.OthersCutoff))
        {
            resourceGroupBreakdown.AddItem(rg.ItemName, Math.Round(rg.Cost, 2), Color.FromInt32(counter++));
        }

        // Render the locations table
        var locationsBreakdown = new BreakdownChart()
            .Width(60);

        counter = 2;
        foreach (var cost in byLocationCosts.TrimList(threshold: settings.OthersCutoff))
        {
            locationsBreakdown.AddItem(cost.ItemName, Math.Round(cost.Cost, 2), Color.FromInt32(counter++));
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
            , new Rows(last7DaysChart,
                nextSevenDaysOfForecast.Any() ? next7DaysChart : new Text("No forecast data available"),
                new Panel(resourceGroupBreakdown).Header("By Resource Group").Expand().Border(BoxBorder.Rounded)));

        subTable.Columns[0].Padding(2, 2).Centered();
        subTable.Columns[1].Padding(2, 2).Centered();

        rootTable.AddRow(subTable);

        AnsiConsole.Write(rootTable);


        return Task.CompletedTask;
    }
}