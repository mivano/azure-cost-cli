"""Tests for output formatters."""
from __future__ import annotations

import io
import json
import sys
from datetime import date, datetime
from types import SimpleNamespace
from unittest.mock import patch

import pytest

from azure_cost_cli.models import (
    AccumulatedCostDetails,
    AnomalyDetectionResult,
    AnomalyType,
    AzureRegion,
    BudgetItem,
    CostDailyItem,
    CostItem,
    CostNamedItem,
    CostResourceItem,
    Notification,
    Subscription,
    SubscriptionPolicies,
)
from azure_cost_cli.formatters.json_formatter import JsonOutputFormatter
from azure_cost_cli.formatters.text_formatter import TextOutputFormatter, _trim_list
from azure_cost_cli.formatters.csv_formatter import CsvOutputFormatter
from azure_cost_cli.formatters.markdown_formatter import MarkdownOutputFormatter


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_details(n_costs: int = 3) -> AccumulatedCostDetails:
    costs = [
        CostItem(date=date(2024, 1, i + 1), cost=float(i + 1) * 10, cost_usd=float(i + 1) * 11, currency="EUR")
        for i in range(n_costs)
    ]
    by_svc = [CostNamedItem("Compute", 20.0, 22.0, "EUR"), CostNamedItem("Storage", 10.0, 11.0, "EUR")]
    by_loc = [CostNamedItem("westeurope", 25.0, 27.5, "EUR"), CostNamedItem("eastus", 5.0, 5.5, "EUR")]
    by_rg = [CostNamedItem("rg1", 28.0, 30.0, "EUR"), CostNamedItem("rg2", 2.0, 2.0, "EUR")]
    sub = Subscription(subscriptionId="abc", displayName="Test Sub", state="Enabled")
    return AccumulatedCostDetails(
        subscription=sub, enrollment_account=None,
        costs=costs, forecasted_costs=[],
        by_service_name_costs=by_svc,
        by_location_costs=by_loc,
        by_resource_group_costs=by_rg,
        by_subscription_costs=None,
    )


def _settings(**overrides):
    defaults = dict(
        use_usd=False, output="Json", query="", skip_header=False,
        others_cutoff=10, subscription="abc", include_tags=False,
        _scope=SimpleNamespace(is_subscription_based=True),
    )
    defaults.update(overrides)
    return SimpleNamespace(**defaults)


# ---------------------------------------------------------------------------
# _trim_list
# ---------------------------------------------------------------------------

def test_trim_list_no_trim():
    items = [CostNamedItem(f"item{i}", float(i), float(i), "USD") for i in range(5)]
    result = _trim_list(items, threshold=10)
    assert len(result) == 5


def test_trim_list_collapses():
    items = [CostNamedItem(f"item{i}", float(i + 1), float(i + 1), "USD") for i in range(15)]
    result = _trim_list(items, threshold=10)
    # 10 top + 1 Others
    assert len(result) == 11
    others = next(x for x in result if x.item_name == "Others")
    assert others.cost > 0


def test_trim_list_zero_threshold():
    items = [CostNamedItem(f"item{i}", float(i), float(i), "USD") for i in range(5)]
    result = _trim_list(items, threshold=0)
    assert len(result) == 5


# ---------------------------------------------------------------------------
# JSON formatter
# ---------------------------------------------------------------------------

def test_json_formatter_accumulated_cost(capsys):
    fmt = JsonOutputFormatter()
    details = _make_details()
    fmt.write_accumulated_cost(_settings(), details)
    captured = capsys.readouterr()
    data = json.loads(captured.out)
    assert "totals" in data
    assert "cost" in data
    assert "byServiceNames" in data
    assert "ByLocation" in data
    assert "ByResourceGroup" in data


def test_json_formatter_budgets(capsys):
    fmt = JsonOutputFormatter()
    from datetime import datetime as dt
    budgets = [BudgetItem(
        name="b1", id="/b1", amount=500.0, time_grain="Monthly",
        start_date=dt(2024, 1, 1), end_date=dt(2024, 12, 31),
        current_spend_amount=200.0, current_spend_currency="USD",
        forecast_amount=400.0, forecast_currency="USD", notifications=[],
    )]
    fmt.write_budgets(_settings(), budgets)
    captured = capsys.readouterr()
    data = json.loads(captured.out)
    assert isinstance(data, list)
    assert data[0]["name"] == "b1"


def test_json_formatter_daily_cost_no_tags(capsys):
    fmt = JsonOutputFormatter()
    items = [
        CostDailyItem(date(2024, 1, 1), "rg1", 10.0, 11.0, "USD"),
        CostDailyItem(date(2024, 1, 1), "rg2", 5.0, 5.5, "USD"),
        CostDailyItem(date(2024, 1, 2), "rg1", 8.0, 9.0, "USD"),
    ]
    fmt.write_daily_cost(_settings(include_tags=False), items)
    captured = capsys.readouterr()
    data = json.loads(captured.out)
    assert len(data) == 2  # 2 unique dates
    assert "Items" in data[0]


def test_json_formatter_daily_cost_with_tags(capsys):
    fmt = JsonOutputFormatter()
    items = [
        CostDailyItem(date(2024, 1, 1), "rg1", 10.0, 11.0, "USD", tags={"env": "prod"}),
    ]
    fmt.write_daily_cost(_settings(include_tags=True), items)
    captured = capsys.readouterr()
    data = json.loads(captured.out)
    assert data[0]["Items"][0]["Tags"] == {"env": "prod"}


def test_json_formatter_anomalies(capsys):
    fmt = JsonOutputFormatter()
    anomalies = [AnomalyDetectionResult(
        name="x", detection_date=date(2024, 5, 1), message="test",
        cost_difference=10.0, anomaly_type=AnomalyType.NEW_COST, data=[],
    )]
    fmt.write_anomaly_detection_results(_settings(), anomalies)
    captured = capsys.readouterr()
    data = json.loads(captured.out)
    assert data[0]["name"] == "x"


def test_json_formatter_diff_cost(capsys):
    fmt = JsonOutputFormatter()
    details1 = _make_details()
    details2 = _make_details()
    # Modify target to have higher costs
    details2.costs[0] = CostItem(details2.costs[0].date, 20.0, 22.0, "EUR")
    fmt.write_accumulated_diff_cost(_settings(), details1, details2)
    captured = capsys.readouterr()
    data = json.loads(captured.out)
    assert "cost" in data


# ---------------------------------------------------------------------------
# Text formatter
# ---------------------------------------------------------------------------

def test_text_formatter_accumulated_cost(capsys):
    fmt = TextOutputFormatter()
    details = _make_details()
    fmt.write_accumulated_cost(_settings(), details)
    captured = capsys.readouterr()
    assert "Azure Cost Overview" in captured.out
    assert "By Service Name" in captured.out
    assert "By Location" in captured.out


def test_text_formatter_no_data(capsys):
    fmt = TextOutputFormatter()
    details = AccumulatedCostDetails(
        subscription=None, enrollment_account=None,
        costs=[], forecasted_costs=[],
        by_service_name_costs=[], by_location_costs=[],
        by_resource_group_costs=[], by_subscription_costs=None,
    )
    fmt.write_accumulated_cost(_settings(), details)
    captured = capsys.readouterr()
    assert "No data found" in captured.out


def test_text_formatter_daily_cost(capsys):
    fmt = TextOutputFormatter()
    items = [
        CostDailyItem(date(2024, 1, 1), "rg1", 10.0, 11.0, "USD"),
        CostDailyItem(date(2024, 1, 2), "rg2", 5.0, 5.5, "USD"),
    ]
    fmt.write_daily_cost(_settings(), items)
    captured = capsys.readouterr()
    assert "2024-01-01" in captured.out
    assert "rg1" in captured.out


def test_text_formatter_anomalies_empty(capsys):
    fmt = TextOutputFormatter()
    fmt.write_anomaly_detection_results(_settings(), [])
    captured = capsys.readouterr()
    assert "No anomalies" in captured.out


def test_text_formatter_budgets(capsys):
    from datetime import datetime as dt
    fmt = TextOutputFormatter()
    budgets = [BudgetItem(
        name="test-budget", id="/b", amount=1000.0, time_grain="Monthly",
        start_date=dt(2024, 1, 1), end_date=dt(2024, 12, 31),
        current_spend_amount=500.0, current_spend_currency="USD",
        forecast_amount=800.0, forecast_currency="USD", notifications=[],
    )]
    fmt.write_budgets(_settings(), budgets)
    captured = capsys.readouterr()
    assert "test-budget" in captured.out
    assert "1000.00" in captured.out


def test_text_formatter_regions(capsys):
    fmt = TextOutputFormatter()
    regions = [AzureRegion(id="westeurope", display_name="West Europe", continent="Europe")]
    fmt.write_regions(_settings(), regions)
    captured = capsys.readouterr()
    assert "West Europe" in captured.out


def test_text_formatter_cost_by_resource(capsys):
    fmt = TextOutputFormatter()
    resources = [CostResourceItem(
        cost=100.0, cost_usd=110.0,
        resource_id="/subscriptions/abc/resourceGroups/rg1/providers/Microsoft.Compute/virtualMachines/vm1",
        resource_type="VM", resource_location="westeurope",
        charge_type="Usage", resource_group_name="rg1",
        publisher_type="Azure", service_name="Compute",
        service_tier="Standard", meter="Standard D2s v5",
        tags={}, currency="USD",
    )]
    fmt.write_cost_by_resource(_settings(), resources, 1, 100.0, "USD")
    captured = capsys.readouterr()
    assert "vm1" in captured.out


# ---------------------------------------------------------------------------
# CSV formatter
# ---------------------------------------------------------------------------

def test_csv_formatter_accumulated_cost(capsys):
    fmt = CsvOutputFormatter()
    details = _make_details()
    fmt.write_accumulated_cost(_settings(), details)
    captured = capsys.readouterr()
    lines = captured.out.strip().split("\n")
    # Header + 3 data rows
    assert len(lines) == 4
    assert "date" in lines[0]


def test_csv_formatter_skip_header(capsys):
    fmt = CsvOutputFormatter()
    details = _make_details(1)
    fmt.write_accumulated_cost(_settings(skip_header=True), details)
    captured = capsys.readouterr()
    lines = captured.out.strip().split("\n")
    # No header row
    assert len(lines) == 1
    assert "date" not in lines[0]


def test_csv_formatter_daily_no_tags(capsys):
    fmt = CsvOutputFormatter()
    items = [
        CostDailyItem(date(2024, 1, 1), "rg1", 10.0, 11.0, "USD"),
        CostDailyItem(date(2024, 1, 2), "rg2", 5.0, 5.5, "USD"),
    ]
    fmt.write_daily_cost(_settings(include_tags=False), items)
    captured = capsys.readouterr()
    lines = captured.out.strip().split("\n")
    assert len(lines) == 3  # header + 2 rows
    assert "name" in lines[0]


def test_csv_formatter_daily_with_tags(capsys):
    fmt = CsvOutputFormatter()
    items = [
        CostDailyItem(date(2024, 1, 1), "rg1", 10.0, 11.0, "USD", tags={"env": "prod"}),
        CostDailyItem(date(2024, 1, 1), "rg2", 5.0, 5.5, "USD", tags={"env": "dev"}),
    ]
    fmt.write_daily_cost(_settings(include_tags=True), items)
    captured = capsys.readouterr()
    lines = captured.out.strip().split("\n")
    assert "env" in lines[0]


def test_csv_formatter_regions(capsys):
    fmt = CsvOutputFormatter()
    regions = [AzureRegion(id="eus", display_name="East US", continent="North America")]
    fmt.write_regions(_settings(), regions)
    captured = capsys.readouterr()
    assert "id" in captured.out
    assert "East US" in captured.out


# ---------------------------------------------------------------------------
# Markdown formatter
# ---------------------------------------------------------------------------

def test_markdown_formatter_accumulated_cost(capsys):
    fmt = MarkdownOutputFormatter()
    details = _make_details()
    fmt.write_accumulated_cost(_settings(), details)
    captured = capsys.readouterr()
    assert "##" in captured.out
    assert "Totals" in captured.out
    assert "|" in captured.out  # Tables present


def test_markdown_formatter_budgets(capsys):
    from datetime import datetime as dt
    fmt = MarkdownOutputFormatter()
    budgets = [BudgetItem(
        name="md-budget", id="/b", amount=2000.0, time_grain="Monthly",
        start_date=dt(2024, 1, 1), end_date=dt(2024, 12, 31),
        current_spend_amount=1000.0, current_spend_currency="USD",
        forecast_amount=1500.0, forecast_currency="USD", notifications=[],
    )]
    fmt.write_budgets(_settings(), budgets)
    captured = capsys.readouterr()
    assert "md-budget" in captured.out


def test_markdown_formatter_regions(capsys):
    fmt = MarkdownOutputFormatter()
    regions = [AzureRegion(id="westus", display_name="West US", continent="North America")]
    fmt.write_regions(_settings(), regions)
    captured = capsys.readouterr()
    assert "West US" in captured.out
    assert "|" in captured.out
