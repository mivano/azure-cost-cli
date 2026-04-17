"""Tests for data models."""
from __future__ import annotations

import pytest
from datetime import date

from azure_cost_cli.models import (
    AnomalyType,
    CostItem,
    CostNamedItem,
    CostDailyItem,
    CostResourceItem,
    BudgetItem,
    Notification,
    Scope,
    TimeframeType,
    MetricType,
    OutputFormat,
    AccumulatedCostDetails,
    AnomalyDetectionResult,
    Subscription,
    SubscriptionPolicies,
)


def test_cost_item():
    item = CostItem(date=date(2024, 1, 15), cost=10.5, cost_usd=11.0, currency="EUR")
    assert item.date == date(2024, 1, 15)
    assert item.cost == 10.5
    assert item.cost_usd == 11.0
    assert item.currency == "EUR"


def test_cost_named_item():
    item = CostNamedItem(item_name="Compute", cost=50.0, cost_usd=55.0, currency="USD")
    assert item.item_name == "Compute"
    assert item.cost == 50.0


def test_cost_daily_item():
    item = CostDailyItem(
        date=date(2024, 1, 1), name="prod-rg", cost=5.0, cost_usd=5.0,
        currency="USD", tags={"env": "prod"}
    )
    assert item.name == "prod-rg"
    assert item.tags == {"env": "prod"}


def test_cost_resource_item_name():
    resource = CostResourceItem(
        cost=100.0, cost_usd=100.0,
        resource_id="/subscriptions/abc/resourceGroups/rg1/providers/Microsoft.Compute/virtualMachines/myVM",
        resource_type="Microsoft.Compute/virtualMachines",
        resource_location="westeurope",
        charge_type="Usage",
        resource_group_name="rg1",
        publisher_type="Azure",
        service_name="Compute",
        service_tier="D2s",
        meter="Standard D2s v5",
        tags={},
        currency="USD",
    )
    assert resource.resource_name == "myVM"


def test_cost_resource_item_empty_id():
    resource = CostResourceItem(
        cost=0.0, cost_usd=0.0, resource_id="",
        resource_type="", resource_location="", charge_type="",
        resource_group_name="", publisher_type="",
        service_name=None, service_tier=None, meter=None,
    )
    assert resource.resource_name == ""


def test_scope_subscription():
    scope = Scope.subscription("abc-123")
    assert scope.scope_path == "/subscriptions/abc-123"
    assert scope.is_subscription_based is True
    assert scope.name == "Subscription"


def test_scope_resource_group():
    scope = Scope.resource_group("sub-id", "my-rg")
    assert "resourceGroups/my-rg" in scope.scope_path
    assert scope.is_subscription_based is True


def test_scope_billing_account():
    scope = Scope.billing_account("ba-123")
    assert "billingAccounts/ba-123" in scope.scope_path
    assert scope.is_subscription_based is False


def test_scope_enrollment_account():
    scope = Scope.enrollment_account("ba-123", "ea-456")
    assert "enrollmentAccounts/ea-456" in scope.scope_path
    assert scope.is_subscription_based is False


def test_timeframe_type_values():
    assert TimeframeType.BILLING_MONTH_TO_DATE.value == "BillingMonthToDate"
    assert TimeframeType.CUSTOM.value == "Custom"
    assert TimeframeType.THE_LAST_MONTH.value == "TheLastMonth"


def test_metric_type_values():
    assert MetricType.ACTUAL_COST.value == "ActualCost"
    assert MetricType.AMORTIZED_COST.value == "AmortizedCost"


def test_output_format_values():
    assert OutputFormat.JSON.value == "Json"
    assert OutputFormat.CSV.value == "Csv"
    assert OutputFormat.CONSOLE.value == "Console"


def test_budget_item():
    from datetime import datetime
    notif = Notification(
        name="over-80", enabled=True, operator="GreaterThan",
        threshold=80.0, contact_emails=["a@b.com"], contact_roles=[], contact_groups=[],
    )
    item = BudgetItem(
        name="monthly-budget", id="/sub/.../budget1", amount=1000.0,
        time_grain="Monthly",
        start_date=datetime(2024, 1, 1),
        end_date=datetime(2024, 12, 31),
        current_spend_amount=750.0,
        current_spend_currency="USD",
        forecast_amount=900.0,
        forecast_currency="USD",
        notifications=[notif],
    )
    assert item.name == "monthly-budget"
    assert item.amount == 1000.0
    assert len(item.notifications) == 1
    assert item.notifications[0].threshold == 80.0


def test_accumulated_cost_details():
    costs = [CostItem(date(2024, 1, 1), 100.0, 110.0, "EUR")]
    details = AccumulatedCostDetails(
        subscription=None, enrollment_account=None,
        costs=costs, forecasted_costs=[],
        by_service_name_costs=[], by_location_costs=[],
        by_resource_group_costs=[], by_subscription_costs=None,
    )
    assert len(details.costs) == 1
    assert details.costs[0].cost == 100.0


def test_anomaly_detection_result():
    result = AnomalyDetectionResult(
        name="compute",
        detection_date=date(2024, 5, 1),
        message="New cost detected",
        cost_difference=50.0,
        anomaly_type=AnomalyType.NEW_COST,
        data=[],
    )
    assert result.anomaly_type == AnomalyType.NEW_COST
    assert result.cost_difference == 50.0


def test_subscription():
    sub = Subscription(
        id="/subscriptions/abc",
        subscriptionId="abc",
        displayName="My Sub",
        state="Enabled",
    )
    assert sub.displayName == "My Sub"
