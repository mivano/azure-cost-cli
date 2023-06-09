using AzureCostCli.CostApi;

namespace AzureCostCli.Infrastructure;

public static class ListExtensions{

    public static List<CostNamedItem> TrimList(this IEnumerable<CostNamedItem> items, int threshold = 10)
    {
        if (threshold<=0 || items.Count() <= threshold)
        {
            return items.OrderByDescending(item => item.Cost).ToList();
        }

        var sortedItems = items.OrderByDescending(item => item.Cost).ToList();
        var topItems = sortedItems.Take(threshold - 1).ToList();
        var otherItems = sortedItems.Skip(threshold - 1);

        var otherItem = new CostNamedItem
        (
            ItemName: "Others",
            Cost: otherItems.Sum(item => item.Cost),
            CostUsd : otherItems.Sum(item => item.CostUsd),
            Currency : topItems.First().Currency
        );

        topItems.Add(otherItem);

        return topItems;
    }
    
  
}