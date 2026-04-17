"""Anomaly detection helper – mirrors CostAnalyzer.cs."""
from __future__ import annotations

from datetime import date, datetime, timedelta, timezone
from typing import Any

from azure_cost_cli.models import AnomalyDetectionResult, AnomalyType, CostDailyItem


def analyze_cost(
    items: list[CostDailyItem],
    recent_activity_days: int = 7,
    significant_change: float = 0.75,
    steady_growth_days: int = 7,
    threshold_cost: float = 2.0,
    exclude_removed_costs: bool = False,
) -> list[AnomalyDetectionResult]:
    from itertools import groupby as _groupby

    today = datetime.now(timezone.utc).date()
    cutoff = today - timedelta(days=recent_activity_days)

    grouped: dict[str, list[CostDailyItem]] = {}
    for item in sorted(items, key=lambda x: x.date):
        grouped.setdefault(item.name, []).append(item)

    results: dict[tuple, AnomalyDetectionResult] = {}

    for name, group in grouped.items():
        group = sorted(group, key=lambda x: x.date)

        # New cost
        if group[0].cost == 0 and any(i.cost != 0 and i.cost > threshold_cost for i in group[1:]):
            start = next(i for i in group if i.cost != 0)
            results[(start.name, AnomalyType.NEW_COST)] = AnomalyDetectionResult(
                name=start.name,
                detection_date=start.date,
                message=f"New cost detected at {start.date}",
                cost_difference=start.cost,
                anomaly_type=AnomalyType.NEW_COST,
                data=group,
            )

        # Removed cost
        if not exclude_removed_costs and group[-1].cost == 0:
            for i in range(len(group) - 2, -1, -1):
                if group[i].cost != 0:
                    end = group[i]
                    det_date = end.date + timedelta(days=1)
                    results[(end.name, AnomalyType.REMOVED_COST)] = AnomalyDetectionResult(
                        name=end.name,
                        detection_date=det_date,
                        message=f"Cost is removed at {det_date}",
                        cost_difference=-end.cost,
                        anomaly_type=AnomalyType.REMOVED_COST,
                        data=group,
                    )
                    break

        # Active resource check
        if not any(i.date >= cutoff for i in group):
            continue

        # Significant change
        for i in range(1, len(group)):
            today_item = group[i]
            yesterday_item = group[i - 1]
            if yesterday_item.cost == 0:
                continue
            diff = today_item.cost - yesterday_item.cost
            if abs(diff) / yesterday_item.cost > significant_change and diff > threshold_cost:
                results[(today_item.name, AnomalyType.SIGNIFICANT_CHANGE)] = AnomalyDetectionResult(
                    name=today_item.name,
                    detection_date=today_item.date,
                    message=(
                        f"Significant cost change detected at {today_item.date} when it went from "
                        f"{yesterday_item.cost:.2f} to {today_item.cost:.2f} (difference of {diff:.2f})"
                    ),
                    cost_difference=diff,
                    anomaly_type=AnomalyType.SIGNIFICANT_CHANGE,
                    data=group,
                )

        # Steady growth
        if len(group) >= steady_growth_days:
            last_n = group[-steady_growth_days:]
            if all(last_n[j].cost > last_n[j - 1].cost for j in range(1, len(last_n))):
                last = group[-1]
                results[(last.name, AnomalyType.STEADY_GROWTH)] = AnomalyDetectionResult(
                    name=last.name,
                    detection_date=last.date,
                    message="Steady cost increase over a week detected",
                    cost_difference=last.cost - group[-steady_growth_days].cost,
                    anomaly_type=AnomalyType.STEADY_GROWTH,
                    data=group,
                )

    return list(results.values())
