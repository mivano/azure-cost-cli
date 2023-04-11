using Spectre.Console;

public class ConsoleOutputFormatter : OutputFormatter
{
    public override Task WriteOutput(ShowSettings settings, IEnumerable<CostItem> costs, IEnumerable<CostItem> forecastedCosts,
        IEnumerable<CostServiceItem> byServiceNameCosts)
    {
        var todaysDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var costToday = costs.FirstOrDefault(a=>a.Date == todaysDate).Cost;
        var costSinceStartOfCurrentMonth = costs.Where(x => x.Date >= todaysDate.AddDays(-todaysDate.Day + 1)).Sum(x => x.Cost); 
        var costYesterday = costs.FirstOrDefault(a=>a.Date == todaysDate.AddDays(-1)).Cost;
        var costLastSevenDays = costs.Where(x => x.Date >= todaysDate.AddDays(-7)).Sum(x => x.Cost);
        var costLastThirtyDays = costs.Where(x => x.Date >= todaysDate.AddDays(-30)).Sum(x => x.Cost);
       
        var currency= costs.FirstOrDefault()?.Currency;
        
        // Create a table
        var table = new Table();
        table.Title = new TableTitle("Azure Costs");
        table.Border(TableBorder.None);
        table.ShowHeaders=false;

// Add some columns
        table.AddColumn("");
        table.AddColumn(new TableColumn("").Centered());

// Add some rows
        table.AddRow("[green bold]Today:[/]", $"{costToday:N2} {currency}");
        table.AddRow("[green bold]Yesterday:[/]", $"{costYesterday:N2} {currency}");
        table.AddRow("[blue bold]Since start month:[/]", $"{costSinceStartOfCurrentMonth:N2} {currency}");
        table.AddRow("[yellow bold]Last 7 days:[/]", $"{costLastSevenDays:N2} {currency}");
        table.AddRow("[yellow bold]Last 30 days:[/]", $"{costLastThirtyDays:N2} {currency}");
        
// Render the table to the console
        AnsiConsole.Write(table);
        
        // Get the last 7 days of costs, starting by the current date, and iterate over them
        // to see if there are any cost spikes
       
        var chart = new BarChart()
            .Width(60)
            .Label("[green bold underline]Last 7 days[/]")
            .CenterLabel();
           
        var lastSevenDays = costs.Where(x => x.Date >= todaysDate.AddDays(-7)).OrderBy(x => x.Date).ToList();
         for (var i = 0; i < lastSevenDays.Count - 1; i++)
        {
            var currentDay = lastSevenDays[i];
            
            chart.AddItem(currentDay.Date.ToString("dd.MM"), Math.Round(currentDay.Cost,2), Color.Green);
        }
        
        AnsiConsole.Write(chart);
        
        // if (hasCostSpike)
        // {
        //     AnsiConsole.MarkupLine("[red bold]Costs have spiked in the last week![/]");
        // }
        // else
        // {
        //     AnsiConsole.MarkupLine("[green bold]Costs have not spiked in the last week.[/]");
        // }
       
        // Render the services table
        var servicesTable = new Table();
        servicesTable.Title = new TableTitle("Azure Costs by Service");
        servicesTable.Border(TableBorder.None);
        servicesTable.ShowHeaders=false;
        servicesTable.AddColumn("Service Name");
        servicesTable.AddColumn(new TableColumn("Cost").Centered());
        
        foreach (var cost in byServiceNameCosts.OrderByDescending(a=>a.Cost))
        {
            servicesTable.AddRow(cost.ServiceName, $"{cost.Cost:N2} {currency}");
        }
        
        AnsiConsole.Write(servicesTable);
        
        return Task.CompletedTask;
    }
}