"""Markdown output formatter – mirrors MarkdownOutputFormatter.cs."""
from __future__ import annotations

from datetime import datetime, timezone
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


class MarkdownOutputFormatter(BaseOutputFormatter):
    def write_accumulated_cost(self, settings: Any, details: AccumulatedCostDetails) -> None:
        skip_header = getattr(settings, "skip_header", False)
        use_usd = getattr(settings, "use_usd", False)

        def pick(item):
            return item.cost_usd if use_usd else item.cost

        if not details.costs:
            if not skip_header:
                print("## Azure Cost Overview")
                print()
            print("No data found")
            return

        today = datetime.now(timezone.utc).date()
        yesterday = today.__class__.fromordinal(today.toordinal() - 1)
        seven_days_ago = today.__class__.fromordinal(today.toordinal() - 7)
        thirty_days_ago = today.__class__.fromordinal(today.toordinal() - 30)

        currency = "USD" if use_usd else (details.costs[0].currency if details.costs else "USD")
        sub_name = details.subscription.displayName if details.subscription else "Unknown"
        min_date = min(c.date for c in details.costs)
        max_date = max(c.date for c in details.costs)

        if not skip_header:
            print(f"## Azure Cost Overview for {sub_name}")
            print(f"> Period: {min_date} – {max_date}")
            print()

        print("### Totals")
        print(f"| Period | Cost |")
        print(f"|--------|------|")
        print(f"| Today | {sum(pick(a) for a in details.costs if a.date == today):.2f} {currency} |")
        print(f"| Yesterday | {sum(pick(a) for a in details.costs if a.date == yesterday):.2f} {currency} |")
        print(f"| Last 7 days | {sum(pick(a) for a in details.costs if a.date >= seven_days_ago):.2f} {currency} |")
        print(f"| Last 30 days | {sum(pick(a) for a in details.costs if a.date >= thirty_days_ago):.2f} {currency} |")
        print(f"| Total in timeframe | {sum(pick(a) for a in details.costs):.2f} {currency} |")
        print()

        cutoff = getattr(settings, "others_cutoff", 10)
        from azure_cost_cli.formatters.text_formatter import _trim_list

        print("### By Service Name")
        print("| Service | Cost |")
        print("|---------|------|")
        for c in _trim_list(sorted(details.by_service_name_costs, key=lambda x: x.cost, reverse=True), cutoff):
            print(f"| {c.item_name} | {pick(c):.2f} {currency} |")
        print()

        print("### By Location")
        print("| Location | Cost |")
        print("|----------|------|")
        for c in _trim_list(sorted(details.by_location_costs, key=lambda x: x.cost, reverse=True), cutoff):
            print(f"| {c.item_name} | {pick(c):.2f} {currency} |")
        print()

        if details.by_subscription_costs:
            print("### By Subscription")
            print("| Subscription | Cost |")
            print("|--------------|------|")
            for c in _trim_list(sorted(details.by_subscription_costs, key=lambda x: x.cost, reverse=True), cutoff):
                print(f"| {c.item_name} | {pick(c):.2f} {currency} |")
            print()

        scope = getattr(settings, "_scope", None)
        is_sub_based = getattr(scope, "is_subscription_based", True)
        if is_sub_based:
            print("### By Resource Group")
            print("| Resource Group | Cost |")
            print("|----------------|------|")
            for c in _trim_list(sorted(details.by_resource_group_costs, key=lambda x: x.cost, reverse=True), cutoff):
                print(f"| {c.item_name} | {pick(c):.2f} {currency} |")
            print()

    def write_cost_by_resource(
        self, settings: Any, resources: list[CostResourceItem],
        total_count: int = 0, total_cost: float = 0, currency: str = "USD",
    ) -> None:
        skip_header = getattr(settings, "skip_header", False)
        use_usd = getattr(settings, "use_usd", False)
        if not skip_header:
            print(f"## Azure Cost by Resource")
            print(f"Total resources: {total_count}, Total: {total_cost:.2f} {currency}")
            print()
        print("| Resource | Type | Location | Cost |")
        print("|----------|------|----------|------|")
        for r in resources:
            cost = r.cost_usd if use_usd else r.cost
            print(f"| {r.resource_name or r.resource_id} | {r.resource_type} | {r.resource_location} | {cost:.2f} {r.currency} |")

    def write_budgets(self, settings: Any, budgets: list[BudgetItem]) -> None:
        skip_header = getattr(settings, "skip_header", False)
        if not skip_header:
            print("## Azure Budgets")
            print()
        print("| Name | Amount | Current Spend | Forecast | Status |")
        print("|------|--------|--------------|----------|--------|")
        for b in budgets:
            spend_pct = (
                f"{b.current_spend_amount / b.amount * 100:.1f}%"
                if b.current_spend_amount is not None and b.amount > 0
                else "N/A"
            )
            print(
                f"| {b.name} | {b.amount:.2f} | "
                f"{b.current_spend_amount} {b.current_spend_currency} ({spend_pct}) | "
                f"{b.forecast_amount} {b.forecast_currency} | |"
            )

    def write_daily_cost(self, settings: Any, daily_costs: list[CostDailyItem]) -> None:
        use_usd = getattr(settings, "use_usd", False)
        from itertools import groupby as _groupby
        sorted_costs = sorted(daily_costs, key=lambda x: x.date)
        print("## Daily Cost")
        print()
        for d, group in _groupby(sorted_costs, key=lambda x: x.date):
            print(f"### {d}")
            print("| Name | Cost | Currency |")
            print("|------|------|----------|")
            for item in group:
                cost = item.cost_usd if use_usd else item.cost
                print(f"| {item.name} | {cost:.2f} | {item.currency} |")
            print()

    def write_anomaly_detection_results(
        self, settings: Any, anomalies: list[AnomalyDetectionResult]
    ) -> None:
        print("## Anomaly Detection Results")
        print()
        if not anomalies:
            print("No anomalies detected.")
            return
        print("| Name | Type | Date | Message | Cost Diff |")
        print("|------|------|------|---------|-----------|")
        for a in anomalies:
            print(f"| {a.name} | {a.anomaly_type.value} | {a.detection_date} | {a.message} | {a.cost_difference:.2f} |")

    def write_regions(self, settings: Any, regions: list[AzureRegion]) -> None:
        print("## Azure Regions")
        print()
        print("| ID | Name | Continent |")
        print("|----|------|-----------|")
        for r in regions:
            print(f"| {r.id} | {r.display_name} | {r.continent} |")

    def write_cost_by_tag(
        self, settings: Any, by_tags: dict[str, dict[str, list[CostResourceItem]]]
    ) -> None:
        use_usd = getattr(settings, "use_usd", False)
        print("## Cost by Tag")
        print()
        for tag_key, tag_values in by_tags.items():
            print(f"### Tag: {tag_key}")
            print("| Value | Total Cost | Currency |")
            print("|-------|-----------|----------|")
            for tag_value, resources in tag_values.items():
                total = sum(r.cost_usd if use_usd else r.cost for r in resources)
                currency = resources[0].currency if resources else "USD"
                print(f"| {tag_value} | {total:.2f} | {currency} |")
            print()

    def write_prices_per_region(
        self, settings: Any, prices_by_region: dict[UsageDetails, list[PriceRecord]]
    ) -> None:
        print("## Prices per Region")
        print()
        for usage, prices in prices_by_region.items():
            name = (usage.properties.resource_name or usage.id) if usage.properties else usage.id
            print(f"### {name}")
            print("| Region | Unit Price | Unit |")
            print("|--------|-----------|------|")
            for p in prices:
                print(f"| {p.arm_region_name} | {p.unit_price:.4f} {p.currency_code} | {p.unit_of_measure} |")
            print()

    def write_dev_test_comparison(self, settings: Any, items: list[DevTestComparisonItem]) -> None:
        print("## Dev/Test Comparison")
        print()
        print("| Resource | Region | Current Cost | Dev/Test Cost | Savings |")
        print("|----------|--------|-------------|--------------|---------|")
        for item in items:
            savings = f"{item.savings:.2f}" if item.savings is not None else "N/A"
            devtest = f"{item.dev_test_cost:.2f}" if item.dev_test_cost is not None else "N/A"
            print(f"| {item.resource_name} | {item.region} | {item.current_cost:.2f} | {devtest} | {savings} |")

    def write_accumulated_diff_cost(
        self, settings: Any, source: AccumulatedCostDetails, target: AccumulatedCostDetails
    ) -> None:
        from azure_cost_cli.formatters.json_formatter import JsonOutputFormatter
        json_fmt = JsonOutputFormatter()
        diff_details = json_fmt._build_diff_details(source, target)
        self.write_accumulated_cost(settings, diff_details)
