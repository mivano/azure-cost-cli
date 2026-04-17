"""CSV output formatter – mirrors CsvOutputFormatter.cs."""
from __future__ import annotations

import csv
import dataclasses
import io
import sys
from datetime import date
from typing import Any

from azure_cost_cli.formatters.base import BaseOutputFormatter
from azure_cost_cli.models import (
    AccumulatedCostDetails,
    AnomalyDetectionResult,
    AzureRegion,
    BudgetItem,
    CostDailyItem,
    CostResourceItem,
    DevTestComparisonItem,
    PriceRecord,
    UsageDetails,
)


def _write_csv(rows: list[dict], skip_header: bool = False) -> None:
    if not rows:
        return
    writer = csv.DictWriter(sys.stdout, fieldnames=list(rows[0].keys()), lineterminator="\n")
    if not skip_header:
        writer.writeheader()
    writer.writerows(rows)


def _item_to_dict(obj: Any) -> dict:
    if dataclasses.is_dataclass(obj) and not isinstance(obj, type):
        result = {}
        for f in dataclasses.fields(obj):
            val = getattr(obj, f.name)
            if isinstance(val, date):
                result[f.name] = val.isoformat()
            elif isinstance(val, (dict, list)):
                result[f.name] = str(val)
            else:
                result[f.name] = val
        return result
    return dict(obj)


class CsvOutputFormatter(BaseOutputFormatter):
    def write_accumulated_cost(self, settings: Any, details: AccumulatedCostDetails) -> None:
        skip_header = getattr(settings, "skip_header", False)
        rows = [
            {"date": c.date.isoformat(), "cost": c.cost, "cost_usd": c.cost_usd, "currency": c.currency}
            for c in details.costs
        ]
        _write_csv(rows, skip_header)

    def write_cost_by_resource(
        self, settings: Any, resources: list[CostResourceItem],
        total_count: int = 0, total_cost: float = 0, currency: str = "USD",
    ) -> None:
        skip_header = getattr(settings, "skip_header", False)
        rows = [_item_to_dict(r) for r in resources]
        _write_csv(rows, skip_header)

    def write_budgets(self, settings: Any, budgets: list[BudgetItem]) -> None:
        skip_header = getattr(settings, "skip_header", False)
        rows = []
        for b in budgets:
            rows.append({
                "name": b.name,
                "id": b.id,
                "amount": b.amount,
                "time_grain": b.time_grain,
                "start_date": b.start_date.isoformat(),
                "end_date": b.end_date.isoformat(),
                "current_spend_amount": b.current_spend_amount,
                "current_spend_currency": b.current_spend_currency,
                "current_spend_percentage": (
                    round(b.current_spend_amount / b.amount * 100, 1)
                    if b.current_spend_amount is not None and b.amount > 0
                    else None
                ),
                "forecast_amount": b.forecast_amount,
                "forecast_currency": b.forecast_currency,
                "forecast_percentage": (
                    round(b.forecast_amount / b.amount * 100, 1)
                    if b.forecast_amount is not None and b.amount > 0
                    else None
                ),
                "remaining": (b.amount - b.current_spend_amount) if b.current_spend_amount is not None else b.amount,
                "notification_count": len(b.notifications),
            })
        _write_csv(rows, skip_header)

    def write_daily_cost(self, settings: Any, daily_costs: list[CostDailyItem]) -> None:
        skip_header = getattr(settings, "skip_header", False)
        include_tags = getattr(settings, "include_tags", False)

        if include_tags:
            all_tag_keys = sorted({k for c in daily_costs for k in (c.tags or {}).keys()})
            rows = []
            for c in daily_costs:
                row: dict[str, Any] = {
                    "name": c.name,
                    "date": c.date.isoformat(),
                    "cost": c.cost,
                    "currency": c.currency,
                    "cost_usd": c.cost_usd,
                }
                for k in all_tag_keys:
                    row[k] = (c.tags or {}).get(k, "")
                rows.append(row)
        else:
            rows = [
                {
                    "name": c.name,
                    "date": c.date.isoformat(),
                    "cost": c.cost,
                    "currency": c.currency,
                    "cost_usd": c.cost_usd,
                }
                for c in daily_costs
            ]
        _write_csv(rows, skip_header)

    def write_anomaly_detection_results(
        self, settings: Any, anomalies: list[AnomalyDetectionResult]
    ) -> None:
        skip_header = getattr(settings, "skip_header", False)
        rows = [
            {
                "name": a.name,
                "detection_date": a.detection_date.isoformat(),
                "message": a.message,
                "cost_difference": a.cost_difference,
                "anomaly_type": a.anomaly_type.value,
            }
            for a in anomalies
        ]
        _write_csv(rows, skip_header)

    def write_regions(self, settings: Any, regions: list[AzureRegion]) -> None:
        skip_header = getattr(settings, "skip_header", False)
        rows = [
            {"id": r.id, "display_name": r.display_name, "continent": r.continent,
             "location": r.location, "latitude": r.latitude, "longitude": r.longitude}
            for r in regions
        ]
        _write_csv(rows, skip_header)

    def write_cost_by_tag(
        self, settings: Any, by_tags: dict[str, dict[str, list[CostResourceItem]]]
    ) -> None:
        skip_header = getattr(settings, "skip_header", False)
        use_usd = getattr(settings, "use_usd", False)
        rows = []
        for tag_key, tag_values in by_tags.items():
            for tag_value, resources in tag_values.items():
                total = sum(r.cost_usd if use_usd else r.cost for r in resources)
                currency = resources[0].currency if resources else "USD"
                rows.append({"tag": tag_key, "value": tag_value, "cost": total, "currency": currency})
        _write_csv(rows, skip_header)

    def write_prices_per_region(
        self, settings: Any, prices_by_region: dict[UsageDetails, list[PriceRecord]]
    ) -> None:
        skip_header = getattr(settings, "skip_header", False)
        rows = []
        for usage, prices in prices_by_region.items():
            name = (usage.properties.resource_name or usage.id) if usage.properties else usage.id
            for p in prices:
                rows.append({
                    "resource": name,
                    "region": p.arm_region_name,
                    "unit_price": p.unit_price,
                    "currency": p.currency_code,
                    "unit_of_measure": p.unit_of_measure,
                })
        _write_csv(rows, skip_header)

    def write_dev_test_comparison(self, settings: Any, items: list[DevTestComparisonItem]) -> None:
        skip_header = getattr(settings, "skip_header", False)
        rows = [_item_to_dict(i) for i in items]
        _write_csv(rows, skip_header)

    def write_accumulated_diff_cost(
        self, settings: Any, source: AccumulatedCostDetails, target: AccumulatedCostDetails
    ) -> None:
        from azure_cost_cli.formatters.json_formatter import JsonOutputFormatter
        json_fmt = JsonOutputFormatter()
        diff_details = json_fmt._build_diff_details(source, target)
        self.write_accumulated_cost(settings, diff_details)
