using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace AzureCostCli.OutputFormatters.SpectreConsole;

public class Money : Renderable
{
    private readonly Markup _paragraph;
    
    public Money(double amount, string currency, Style? style = null, Justify justify = Justify.Right)
    {

        _paragraph = new Markup(FormatMoney(amount, currency), style)
        {
            Justification = justify
        };
    }

    /// <inheritdoc/>
    protected override IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
    {
        return ((IRenderable)_paragraph).Render(options, maxWidth);
    }

    public static string FormatMoney(double amount, string currency)
    {
       
// Get current culture info
        var cultureInfo = CultureInfo.CurrentCulture;
// Get culture specific decimal separator
        var decimalSeparator = cultureInfo.NumberFormat.NumberDecimalSeparator;
// Get culture specific thousand separator
        var thousandSeparator = cultureInfo.NumberFormat.NumberGroupSeparator;

// Split the amount into integer and fraction parts
        var amountParts = amount.ToString("N2", cultureInfo).Split(decimalSeparator);
        string amountInteger = amountParts[0];
        string amountFraction = amountParts.Length > 1 ? amountParts[1] : "00";

// Prepare styled string
        string styledAmount = $"[bold dim]{amountInteger}[/]{decimalSeparator}[dim]{amountFraction}[/] [green]{currency}[/]";

        return styledAmount;

    }
    
}