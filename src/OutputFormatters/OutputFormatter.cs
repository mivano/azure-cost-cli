public abstract class OutputFormatter
{
    public abstract Task WriteOutput(ShowSettings settings, IEnumerable<CostItem> costs, IEnumerable<CostItem> forecastedCosts, IEnumerable<CostServiceItem> byServiceNameCosts);
}