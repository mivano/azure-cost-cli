"""JSON output formatter – mirrors JsonOutputFormatter.cs."""
from __future__ import annotations

import dataclasses
import json
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
    OutputFormat,
    PriceRecord,
    UsageDetails,
)


def _date_default(obj: Any) -> str:
    if isinstance(obj, date):
        return obj.isoformat()
    raise TypeError(f"Object of type {type(obj)} is not JSON serializable")


def _to_dict(obj: Any) -> Any:
    if dataclasses.is_dataclass(obj) and not isinstance(obj, type):
        return {k: _to_dict(v) for k, v in dataclasses.asdict(obj).items()}
    if isinstance(obj, list):
        return [_to_dict(i) for i in obj]
    if isinstance(obj, dict):
        return {k: _to_dict(v) for k, v in obj.items()}
    if isinstance(obj, date):
        return obj.isoformat()
    return obj


def _apply_jmespath(json_str: str, query: str) -> str:
    try:
        import jmespath  # type: ignore
        data = json.loads(json_str)
        result = jmespath.search(query, data)
        return json.dumps(result, indent=2, default=_date_default)
    except Exception:
        return json_str


class JsonOutputFormatter(BaseOutputFormatter):
    def __init__(self, colored: bool = False):
        self.colored = colored

    def _write(self, settings: Any, obj: Any) -> None:
        data = _to_dict(obj)
        json_str = json.dumps(data, indent=2, default=_date_default)

        query = getattr(settings, "query", "") or ""
        if query:
            json_str = _apply_jmespath(json_str, query)

        if self.colored:
            try:
                from rich.console import Console
                from rich.syntax import Syntax

                console = Console()
                console.print(Syntax(json_str, "json", theme="monokai"))
                return
            except ImportError:
                pass  # fall through to plain output

        print(json_str, end="")

    def write_accumulated_cost(self, settings: Any, details: AccumulatedCostDetails) -> None:
        from datetime import datetime, timezone

        today = datetime.now(timezone.utc).date()
        seven_days_ago = datetime.now(timezone.utc).date().__class__.fromordinal(
            today.toordinal() - 7
        )
        thirty_days_ago = today.__class__.fromordinal(today.toordinal() - 30)
        yesterday = today.__class__.fromordinal(today.toordinal() - 1)

        use_usd = getattr(settings, "use_usd", False)

        def pick(item):
            return item.cost_usd if use_usd else item.cost

        output = {
            "totals": {
                "todaysCost": sum(pick(a) for a in details.costs if a.date == today),
                "yesterdayCost": sum(pick(a) for a in details.costs if a.date == yesterday),
                "lastSevenDaysCost": sum(pick(a) for a in details.costs if a.date >= seven_days_ago),
                "lastThirtyDaysCost": sum(pick(a) for a in details.costs if a.date >= thirty_days_ago),
                "totalCostInTimeframe": sum(pick(a) for a in details.costs),
            },
            "cost": sorted(
                [{"Date": a.date, "Cost": a.cost, "Currency": a.currency, "CostUsd": a.cost_usd}
                 for a in details.costs],
                key=lambda x: x["Date"],
            ),
            "forecastedCosts": sorted(
                [{"Date": a.date, "Cost": a.cost, "Currency": a.currency, "CostUsd": a.cost_usd}
                 for a in details.forecasted_costs],
                key=lambda x: x["Date"],
                reverse=True,
            ),
            "byServiceNames": sorted(
                [{"ServiceName": a.item_name, "Cost": a.cost, "Currency": a.currency, "CostUsd": a.cost_usd}
                 for a in details.by_service_name_costs],
                key=lambda x: x["Cost"],
                reverse=True,
            ),
            "ByLocation": sorted(
                [{"Location": a.item_name, "Cost": a.cost, "Currency": a.currency, "CostUsd": a.cost_usd}
                 for a in details.by_location_costs],
                key=lambda x: x["Cost"],
                reverse=True,
            ),
            "ByResourceGroup": sorted(
                [{"ResourceGroup": a.item_name, "Cost": a.cost, "Currency": a.currency, "CostUsd": a.cost_usd}
                 for a in details.by_resource_group_costs],
                key=lambda x: x["Cost"],
                reverse=True,
            ),
        }
        self._write(settings, output)

    def write_cost_by_resource(
        self, settings: Any, resources: list[CostResourceItem],
        total_count: int = 0, total_cost: float = 0, currency: str = "USD",
    ) -> None:
        self._write(settings, resources)

    def write_budgets(self, settings: Any, budgets: list[BudgetItem]) -> None:
        self._write(settings, budgets)

    def write_daily_cost(self, settings: Any, daily_costs: list[CostDailyItem]) -> None:
        include_tags = getattr(settings, "include_tags", False)

        from itertools import groupby as _groupby
        sorted_costs = sorted(daily_costs, key=lambda x: x.date)
        output = []
        for d, group in _groupby(sorted_costs, key=lambda x: x.date):
            items = []
            for b in group:
                if include_tags:
                    items.append({"Name": b.name, "Cost": b.cost, "Currency": b.currency,
                                  "CostUsd": b.cost_usd, "Tags": b.tags or {}})
                else:
                    items.append({"Name": b.name, "Cost": b.cost, "Currency": b.currency,
                                  "CostUsd": b.cost_usd})
            output.append({"Date": d, "Items": items})
        self._write(settings, output)

    def write_anomaly_detection_results(
        self, settings: Any, anomalies: list[AnomalyDetectionResult]
    ) -> None:
        self._write(settings, anomalies)

    def write_regions(self, settings: Any, regions: list[AzureRegion]) -> None:
        self._write(settings, regions)

    def write_cost_by_tag(
        self, settings: Any, by_tags: dict[str, dict[str, list[CostResourceItem]]]
    ) -> None:
        self._write(settings, by_tags)

    def write_prices_per_region(
        self, settings: Any, prices_by_region: dict[UsageDetails, list[PriceRecord]]
    ) -> None:
        output = [
            {"UsageDetails": k, "Regions": v}
            for k, v in prices_by_region.items()
        ]
        self._write(settings, output)

    def write_dev_test_comparison(self, settings: Any, items: list[DevTestComparisonItem]) -> None:
        self._write(settings, items)

    @staticmethod
    def _build_diff_details(source: AccumulatedCostDetails, target: AccumulatedCostDetails) -> AccumulatedCostDetails:
        from azure_cost_cli.models import CostItem, CostNamedItem, AccumulatedCostDetails as ACD

        source_by_date = {a.date: a for a in source.costs}
        target_by_date = {a.date: a for a in target.costs}
        cost_diff = [
            CostItem(
                date=d,
                cost=target_by_date[d].cost - source_by_date[d].cost,
                cost_usd=target_by_date[d].cost_usd - source_by_date[d].cost_usd,
                currency=source_by_date[d].currency,
            )
            for d in source_by_date if d in target_by_date
        ]

        source_fc = {a.date: a for a in source.forecasted_costs}
        target_fc = {a.date: a for a in target.forecasted_costs}
        fc_diff = [
            CostItem(
                date=d,
                cost=target_fc[d].cost - source_fc[d].cost,
                cost_usd=target_fc[d].cost_usd - source_fc[d].cost_usd,
                currency=source_fc[d].currency,
            )
            for d in source_fc if d in target_fc
        ]

        def named_diff(src_list, tgt_list):
            tgt = {a.item_name: a for a in tgt_list}
            return [
                CostNamedItem(
                    item_name=a.item_name,
                    cost=tgt[a.item_name].cost - a.cost,
                    cost_usd=tgt[a.item_name].cost_usd - a.cost_usd,
                    currency=a.currency,
                )
                for a in src_list if a.item_name in tgt
            ]

        return ACD(
            subscription=None,
            enrollment_account=None,
            costs=[x for x in cost_diff if x.cost != 0],
            forecasted_costs=[x for x in fc_diff if x.cost != 0],
            by_service_name_costs=[x for x in named_diff(source.by_service_name_costs, target.by_service_name_costs) if x.cost != 0],
            by_location_costs=[x for x in named_diff(source.by_location_costs, target.by_location_costs) if x.cost != 0],
            by_resource_group_costs=[x for x in named_diff(source.by_resource_group_costs, target.by_resource_group_costs) if x.cost != 0],
            by_subscription_costs=None,
        )

    def write_accumulated_diff_cost(
        self, settings: Any, source: AccumulatedCostDetails, target: AccumulatedCostDetails
    ) -> None:
        diff_details = self._build_diff_details(source, target)
        from types import SimpleNamespace
        diff_settings = SimpleNamespace(
            use_usd=getattr(settings, "use_usd", False),
            output=getattr(settings, "output", "Json"),
            query=getattr(settings, "query", ""),
        )
        self.write_accumulated_cost(diff_settings, diff_details)
