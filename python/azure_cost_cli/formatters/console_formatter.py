"""Rich console output formatter – mirrors ConsoleOutputFormatter.cs."""
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


def _get_console():
    try:
        from rich.console import Console
        return Console()
    except ImportError:
        return None


class ConsoleOutputFormatter(BaseOutputFormatter):
    def write_accumulated_cost(self, settings: Any, details: AccumulatedCostDetails) -> None:
        console = _get_console()
        use_usd = getattr(settings, "use_usd", False)

        def pick(item):
            return item.cost_usd if use_usd else item.cost

        if not details.costs:
            if console:
                console.print("[bold]Azure Cost Overview[/bold]")
                console.print("No data found")
            else:
                print("Azure Cost Overview\nNo data found")
            return

        today = datetime.now(timezone.utc).date()
        yesterday = today.__class__.fromordinal(today.toordinal() - 1)
        seven_days_ago = today.__class__.fromordinal(today.toordinal() - 7)
        thirty_days_ago = today.__class__.fromordinal(today.toordinal() - 30)

        currency = "USD" if use_usd else (details.costs[0].currency if details.costs else "USD")
        sub_name = details.subscription.displayName if details.subscription else "Unknown"
        min_date = min(c.date for c in details.costs)
        max_date = max(c.date for c in details.costs)

        if console:
            from rich.table import Table
            console.print(f"[bold cyan]Azure Cost Overview for {sub_name}[/bold cyan]")
            console.print(f"Period: {min_date} – {max_date}")
            console.print()

            totals_table = Table(title="Totals", show_header=True)
            totals_table.add_column("Period", style="cyan")
            totals_table.add_column("Cost", justify="right", style="green")
            totals_table.add_row("Today", f"{sum(pick(a) for a in details.costs if a.date == today):.2f} {currency}")
            totals_table.add_row("Yesterday", f"{sum(pick(a) for a in details.costs if a.date == yesterday):.2f} {currency}")
            totals_table.add_row("Last 7 days", f"{sum(pick(a) for a in details.costs if a.date >= seven_days_ago):.2f} {currency}")
            totals_table.add_row("Last 30 days", f"{sum(pick(a) for a in details.costs if a.date >= thirty_days_ago):.2f} {currency}")
            totals_table.add_row("[bold]Total in timeframe[/bold]", f"[bold]{sum(pick(a) for a in details.costs):.2f} {currency}[/bold]")
            console.print(totals_table)

            cutoff = getattr(settings, "others_cutoff", 10)
            from azure_cost_cli.formatters.text_formatter import _trim_list

            svc_table = Table(title="By Service Name", show_header=True)
            svc_table.add_column("Service", style="cyan")
            svc_table.add_column("Cost", justify="right", style="green")
            for c in _trim_list(sorted(details.by_service_name_costs, key=lambda x: x.cost, reverse=True), cutoff):
                svc_table.add_row(c.item_name, f"{pick(c):.2f} {currency}")
            console.print(svc_table)

            loc_table = Table(title="By Location", show_header=True)
            loc_table.add_column("Location", style="cyan")
            loc_table.add_column("Cost", justify="right", style="green")
            for c in _trim_list(sorted(details.by_location_costs, key=lambda x: x.cost, reverse=True), cutoff):
                loc_table.add_row(c.item_name, f"{pick(c):.2f} {currency}")
            console.print(loc_table)

            scope = getattr(settings, "_scope", None)
            is_sub_based = getattr(scope, "is_subscription_based", True)
            if is_sub_based:
                rg_table = Table(title="By Resource Group", show_header=True)
                rg_table.add_column("Resource Group", style="cyan")
                rg_table.add_column("Cost", justify="right", style="green")
                for c in _trim_list(sorted(details.by_resource_group_costs, key=lambda x: x.cost, reverse=True), cutoff):
                    rg_table.add_row(c.item_name, f"{pick(c):.2f} {currency}")
                console.print(rg_table)
        else:
            from azure_cost_cli.formatters.text_formatter import TextOutputFormatter
            TextOutputFormatter().write_accumulated_cost(settings, details)

    def write_cost_by_resource(
        self, settings: Any, resources: list[CostResourceItem],
        total_count: int = 0, total_cost: float = 0, currency: str = "USD",
    ) -> None:
        console = _get_console()
        use_usd = getattr(settings, "use_usd", False)
        if console:
            from rich.table import Table
            table = Table(title=f"Cost by Resource (Total: {total_cost:.2f} {currency})")
            table.add_column("Resource", style="cyan")
            table.add_column("Type", style="dim")
            table.add_column("Location", style="dim")
            table.add_column("Cost", justify="right", style="green")
            for r in resources:
                cost = r.cost_usd if use_usd else r.cost
                table.add_row(r.resource_name or r.resource_id, r.resource_type, r.resource_location, f"{cost:.2f} {r.currency}")
            console.print(table)
        else:
            from azure_cost_cli.formatters.text_formatter import TextOutputFormatter
            TextOutputFormatter().write_cost_by_resource(settings, resources, total_count, total_cost, currency)

    def write_budgets(self, settings: Any, budgets: list[BudgetItem]) -> None:
        console = _get_console()
        if console:
            from rich.table import Table
            table = Table(title="Azure Budgets")
            table.add_column("Name")
            table.add_column("Amount", justify="right")
            table.add_column("Current Spend", justify="right")
            table.add_column("Forecast", justify="right")
            for b in budgets:
                spend_pct = (
                    f" ({b.current_spend_amount / b.amount * 100:.1f}%)"
                    if b.current_spend_amount is not None and b.amount > 0 else ""
                )
                table.add_row(
                    b.name,
                    f"{b.amount:.2f}",
                    f"{b.current_spend_amount}{spend_pct}",
                    str(b.forecast_amount),
                )
            console.print(table)
        else:
            from azure_cost_cli.formatters.text_formatter import TextOutputFormatter
            TextOutputFormatter().write_budgets(settings, budgets)

    def write_daily_cost(self, settings: Any, daily_costs: list[CostDailyItem]) -> None:
        from azure_cost_cli.formatters.text_formatter import TextOutputFormatter
        TextOutputFormatter().write_daily_cost(settings, daily_costs)

    def write_anomaly_detection_results(
        self, settings: Any, anomalies: list[AnomalyDetectionResult]
    ) -> None:
        console = _get_console()
        if console:
            from rich.table import Table
            table = Table(title="Anomaly Detection")
            table.add_column("Name")
            table.add_column("Type")
            table.add_column("Date")
            table.add_column("Message")
            table.add_column("Cost Diff", justify="right")
            for a in anomalies:
                table.add_row(a.name, a.anomaly_type.value, str(a.detection_date), a.message, f"{a.cost_difference:.2f}")
            console.print(table)
        else:
            from azure_cost_cli.formatters.text_formatter import TextOutputFormatter
            TextOutputFormatter().write_anomaly_detection_results(settings, anomalies)

    def write_regions(self, settings: Any, regions: list[AzureRegion]) -> None:
        from azure_cost_cli.formatters.text_formatter import TextOutputFormatter
        TextOutputFormatter().write_regions(settings, regions)

    def write_cost_by_tag(
        self, settings: Any, by_tags: dict[str, dict[str, list[CostResourceItem]]]
    ) -> None:
        from azure_cost_cli.formatters.text_formatter import TextOutputFormatter
        TextOutputFormatter().write_cost_by_tag(settings, by_tags)

    def write_prices_per_region(
        self, settings: Any, prices_by_region: dict[UsageDetails, list[PriceRecord]]
    ) -> None:
        from azure_cost_cli.formatters.text_formatter import TextOutputFormatter
        TextOutputFormatter().write_prices_per_region(settings, prices_by_region)

    def write_dev_test_comparison(self, settings: Any, items: list[DevTestComparisonItem]) -> None:
        from azure_cost_cli.formatters.text_formatter import TextOutputFormatter
        TextOutputFormatter().write_dev_test_comparison(settings, items)

    def write_accumulated_diff_cost(
        self, settings: Any, source: AccumulatedCostDetails, target: AccumulatedCostDetails
    ) -> None:
        from azure_cost_cli.formatters.json_formatter import JsonOutputFormatter
        json_fmt = JsonOutputFormatter()
        diff_details = json_fmt._build_diff_details(source, target)
        self.write_accumulated_cost(settings, diff_details)
