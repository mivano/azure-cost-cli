"""Tests for the cost analyzer (anomaly detection)."""
from __future__ import annotations

import pytest
from datetime import date, timedelta

from azure_cost_cli.models import AnomalyType, CostDailyItem
from azure_cost_cli.commands.cost_analyzer import analyze_cost


def _make_items(name: str, costs: list[float], start: date = date(2024, 1, 1)) -> list[CostDailyItem]:
    return [
        CostDailyItem(date=start + timedelta(days=i), name=name, cost=c, cost_usd=c, currency="USD")
        for i, c in enumerate(costs)
    ]


def test_no_anomalies():
    items = _make_items("rg1", [10.0] * 10)
    results = analyze_cost(items, recent_activity_days=365)
    # All costs are the same – no significant change, no new/removed
    anomaly_types = {r.anomaly_type for r in results}
    assert AnomalyType.SIGNIFICANT_CHANGE not in anomaly_types


def test_new_cost_detection():
    # First day zero, then cost appears
    today = date.today()
    start = today - timedelta(days=10)
    items = _make_items("newrg", [0.0, 0.0, 5.0, 5.0, 5.0, 5.0, 5.0, 5.0, 5.0, 5.0], start)
    results = analyze_cost(items, threshold_cost=2.0, recent_activity_days=30)
    new_cost_results = [r for r in results if r.anomaly_type == AnomalyType.NEW_COST]
    assert len(new_cost_results) == 1
    assert new_cost_results[0].name == "newrg"


def test_removed_cost_detection():
    today = date.today()
    start = today - timedelta(days=10)
    # Cost vanishes at end
    items = _make_items("gone", [10.0, 10.0, 10.0, 10.0, 10.0, 0.0, 0.0, 0.0, 0.0, 0.0], start)
    results = analyze_cost(items, recent_activity_days=30)
    removed = [r for r in results if r.anomaly_type == AnomalyType.REMOVED_COST]
    assert len(removed) == 1


def test_removed_cost_excluded():
    today = date.today()
    start = today - timedelta(days=10)
    items = _make_items("gone2", [10.0, 10.0, 10.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0], start)
    results = analyze_cost(items, recent_activity_days=30, exclude_removed_costs=True)
    removed = [r for r in results if r.anomaly_type == AnomalyType.REMOVED_COST]
    assert len(removed) == 0


def test_significant_change():
    today = date.today()
    start = today - timedelta(days=5)
    # Doubles on day 3
    items = _make_items("spike", [10.0, 10.0, 25.0, 25.0, 25.0], start)
    results = analyze_cost(items, significant_change=0.5, threshold_cost=2.0, recent_activity_days=30)
    sig = [r for r in results if r.anomaly_type == AnomalyType.SIGNIFICANT_CHANGE]
    assert len(sig) >= 1
    assert sig[0].cost_difference > 0


def test_steady_growth():
    today = date.today()
    start = today - timedelta(days=9)
    # Steadily increasing costs
    items = _make_items("growing", [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0], start)
    results = analyze_cost(items, steady_growth_days=7, recent_activity_days=30)
    steady = [r for r in results if r.anomaly_type == AnomalyType.STEADY_GROWTH]
    assert len(steady) == 1


def test_empty_items():
    results = analyze_cost([])
    assert results == []


def test_single_item():
    today = date.today()
    items = [CostDailyItem(date=today, name="x", cost=5.0, cost_usd=5.0, currency="USD")]
    results = analyze_cost(items, recent_activity_days=30)
    # Single item: can't trigger most anomalies
    assert isinstance(results, list)


def test_inactive_resource_not_flagged():
    # All items older than recent_activity_days — no significant change reported
    start = date(2023, 1, 1)
    items = _make_items("old", [10.0, 10.0, 25.0, 10.0, 10.0], start)
    results = analyze_cost(items, recent_activity_days=7, significant_change=0.5, threshold_cost=2.0)
    sig = [r for r in results if r.anomaly_type == AnomalyType.SIGNIFICANT_CHANGE]
    assert len(sig) == 0
