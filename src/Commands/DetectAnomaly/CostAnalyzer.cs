using AzureCostCli.CostApi;

namespace AzureCostCli.Commands.ShowCommand;

public class CostAnalyzer
{
    private int RecentActivityDays = 7;
    private double SignificantChange = 0.5; // 50%
    private int SteadyGrowthDays = 7;
    private double ThresholdCost = 2.00d;
    
    public CostAnalyzer(DetectAnomalySettings settings)
    {
        RecentActivityDays = settings.RecentActivityDays;
        SignificantChange = settings.SignificantChange;
        SteadyGrowthDays = settings.SteadyGrowthDays;
        ThresholdCost = settings.ThresholdCost;
    }

    public List<AnomalyDetectionResult> AnalyzeCost(List<CostDailyItem> items)
    {
        var groupedItems = items
            .GroupBy(i => i.Name)
            .Select(g => g.OrderBy(i => i.Date).ToList())
            .ToList();

        var results = new Dictionary<(string, AnomalyType), AnomalyDetectionResult>();
        foreach (var group in groupedItems)
        {
            AnomalyDetectionResult result;

            // Check for new costs
            if (group.First().Cost == 0 && group.Skip(1).Any(i => i.Cost != 0 && i.Cost > ThresholdCost))
            {
                var startCostItem = group.First(i => i.Cost != 0);
                result = new AnomalyDetectionResult
                {
                    Name = startCostItem.Name,
                    DetectionDate = startCostItem.Date,
                    Message = "New cost detected at " + startCostItem.Date,
                    CostDifference = startCostItem.Cost,
                    AnomalyType = AnomalyType.NewCost,
                    Data = group
                };
                results[(startCostItem.Name, AnomalyType.NewCost)] = result;
            }

            // Check for removed costs
            if (group.Last().Cost == 0)
            {
                for (int i = group.Count - 2; i >= 0; i--)
                {
                    if (group[i].Cost != 0)
                    {
                        var endCostItem = group[i];
                        var detectionDate = endCostItem.Date.AddDays(1);
                        result = new AnomalyDetectionResult
                        {
                            Name = endCostItem.Name,
                            DetectionDate = detectionDate, // Assuming the cost was removed the next day
                            Message = $"Cost is removed at {detectionDate}",
                            CostDifference = -endCostItem.Cost,
                            AnomalyType = AnomalyType.RemovedCost,
                            Data = group
                        };
                        results[(endCostItem.Name, AnomalyType.RemovedCost)] = result;
                        break;
                    }
                }
            }

            // Filter out inactive resources
            if (!IsResourceActive(group)) continue;

            // Check for significant cost changes
            for (int i = 1; i < group.Count; i++)
            {
                var today = group[i];
                var yesterday = group[i - 1];
                var diff = today.Cost - yesterday.Cost;

                if (Math.Abs(diff) / yesterday.Cost > SignificantChange && diff > ThresholdCost)
                {
                    result = new AnomalyDetectionResult
                    {
                        Name = today.Name,
                        DetectionDate = today.Date,
                        Message = $"Significant cost change detected at {today.Date} when it went from {yesterday.Cost:N2} to {today.Cost:N2} (difference of {diff:N2})",
                        CostDifference = diff,
                        AnomalyType = AnomalyType.SignificantChange,
                        Data = group
                    };
                    results[(today.Name, AnomalyType.SignificantChange)] = result;
                }
            }

            // Check for steady cost increase over a week
            if (IsSteadyCostIncrease(group))
            {
                var today = group.Last();
                result = new AnomalyDetectionResult
                {
                    Name = today.Name,
                    DetectionDate = today.Date,
                    Message = "Steady cost increase over a week detected",
                    CostDifference = today.Cost - group[^SteadyGrowthDays].Cost,
                    AnomalyType = AnomalyType.SteadyGrowth,
                    Data = group
                };
                results[(today.Name, AnomalyType.SteadyGrowth)] = result;
            }
        }

        return results.Values.ToList();
    }

    private bool IsResourceActive(IEnumerable<CostDailyItem> items)
    {
        return items.Any(i => i.Date >= DateOnly.FromDateTime(DateTime.Now.AddDays(-RecentActivityDays)));
    }

    private bool IsSteadyCostIncrease(IReadOnlyCollection<CostDailyItem> items)
    {
        if (items.Count < SteadyGrowthDays) return false;

        var lastWeekItems = items.TakeLast(SteadyGrowthDays).ToList();
        for (int i = 1; i < lastWeekItems.Count; i++)
        {
            if (lastWeekItems[i].Cost <= lastWeekItems[i - 1].Cost) return false;
        }

        return true;
    }
}