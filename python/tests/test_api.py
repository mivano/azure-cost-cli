"""Tests for the FastAPI REST API endpoints.

All external calls (Azure Cost API, Azure Regions API, Azure Price API) are
mocked so the tests run without real Azure credentials.
"""
from __future__ import annotations

from datetime import date, datetime
from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from fastapi.testclient import TestClient

from azure_cost_cli.api.app import app
from azure_cost_cli.models import (
    AnomalyDetectionResult,
    AnomalyType,
    AzureRegion,
    BudgetItem,
    CostDailyItem,
    CostItem,
    CostNamedItem,
    CostResourceItem,
    MetricType,
    Notification,
    Scope,
    Subscription,
    TimeframeType,
)

client = TestClient(app, raise_server_exceptions=True)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

SUB_ID = "00000000-0000-0000-0000-000000000001"
TODAY = date(2024, 3, 15)
YESTERDAY = date(2024, 3, 14)

_SUBSCRIPTION = Subscription(subscriptionId=SUB_ID, displayName="Test Sub", state="Active")
_COST_ITEMS = [
    CostItem(date=TODAY, cost=100.0, cost_usd=110.0, currency="EUR"),
    CostItem(date=YESTERDAY, cost=80.0, cost_usd=88.0, currency="EUR"),
]
_BY_SVC = [CostNamedItem(item_name="Virtual Machines", cost=120.0, cost_usd=132.0, currency="EUR")]
_BY_LOC = [CostNamedItem(item_name="westeurope", cost=120.0, cost_usd=132.0, currency="EUR")]
_BY_RG = [CostNamedItem(item_name="my-rg", cost=120.0, cost_usd=132.0, currency="EUR")]


def _mock_retriever(
    costs=None,
    forecasted=None,
    by_svc=None,
    by_loc=None,
    by_rg=None,
    by_sub=None,
    daily=None,
    resources=None,
    budgets=None,
    usage=None,
    subscription=None,
):
    r = MagicMock()
    r.retrieve_subscription = AsyncMock(return_value=subscription or _SUBSCRIPTION)
    r.retrieve_costs = AsyncMock(return_value=costs if costs is not None else _COST_ITEMS)
    r.retrieve_forecasted_costs = AsyncMock(return_value=forecasted or [])
    r.retrieve_cost_by_service_name = AsyncMock(return_value=by_svc if by_svc is not None else _BY_SVC)
    r.retrieve_cost_by_location = AsyncMock(return_value=by_loc if by_loc is not None else _BY_LOC)
    r.retrieve_cost_by_resource_group = AsyncMock(return_value=by_rg if by_rg is not None else _BY_RG)
    r.retrieve_cost_by_subscription = AsyncMock(return_value=by_sub or [])
    r.retrieve_daily_cost = AsyncMock(return_value=daily or [])
    r.retrieve_cost_for_resources = AsyncMock(return_value=resources or [])
    r.retrieve_budgets = AsyncMock(return_value=budgets or [])
    r.retrieve_usage_details = AsyncMock(return_value=usage or [])
    r.close = AsyncMock()
    return r


# ---------------------------------------------------------------------------
# /health
# ---------------------------------------------------------------------------

def test_health():
    resp = client.get("/health")
    assert resp.status_code == 200
    body = resp.json()
    assert body["status"] == "ok"
    assert "version" in body


def test_root_redirect():
    resp = client.get("/")
    assert resp.status_code == 200
    assert "docs" in resp.json()


# ---------------------------------------------------------------------------
# /accumulated-cost
# ---------------------------------------------------------------------------

@patch("azure_cost_cli.api.routes.accumulated_cost.make_retriever")
@patch("azure_cost_cli.api.routes.accumulated_cost.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.accumulated_cost.get_scope")
def test_accumulated_cost_basic(mock_scope, mock_sub, mock_make):
    scope = Scope.subscription(SUB_ID)
    mock_scope.return_value = scope
    mock_make.return_value = _mock_retriever()

    resp = client.get(f"/accumulated-cost?subscription={SUB_ID}")
    assert resp.status_code == 200
    body = resp.json()
    assert "totals" in body
    assert "costs" in body
    assert body["totals"]["totalCostInTimeframe"] == pytest.approx(180.0)
    assert len(body["costs"]) == 2
    assert len(body["byServiceNames"]) == 1
    assert body["byServiceNames"][0]["name"] == "Virtual Machines"


@patch("azure_cost_cli.api.routes.accumulated_cost.make_retriever")
@patch("azure_cost_cli.api.routes.accumulated_cost.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.accumulated_cost.get_scope")
def test_accumulated_cost_use_usd(mock_scope, mock_sub, mock_make):
    scope = Scope.subscription(SUB_ID)
    mock_scope.return_value = scope
    mock_make.return_value = _mock_retriever()

    resp = client.get(f"/accumulated-cost?subscription={SUB_ID}&useUsd=true")
    assert resp.status_code == 200
    body = resp.json()
    # costUsd sum = 110 + 88 = 198
    assert body["totals"]["totalCostInTimeframe"] == pytest.approx(198.0)


# ---------------------------------------------------------------------------
# /daily-costs
# ---------------------------------------------------------------------------

@patch("azure_cost_cli.api.routes.daily_costs.make_retriever")
@patch("azure_cost_cli.api.routes.daily_costs.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.daily_costs.get_scope")
def test_daily_costs(mock_scope, mock_sub, mock_make):
    scope = Scope.subscription(SUB_ID)
    mock_scope.return_value = scope
    daily = [
        CostDailyItem(date=TODAY, name="rg1", cost=50.0, cost_usd=55.0, currency="EUR"),
        CostDailyItem(date=TODAY, name="rg2", cost=30.0, cost_usd=33.0, currency="EUR"),
        CostDailyItem(date=YESTERDAY, name="rg1", cost=40.0, cost_usd=44.0, currency="EUR"),
    ]
    mock_make.return_value = _mock_retriever(daily=daily)

    resp = client.get(f"/daily-costs?subscription={SUB_ID}&dimension=ResourceGroupName")
    assert resp.status_code == 200
    body = resp.json()
    # Two unique dates
    assert len(body) == 2
    dates = {g["date"] for g in body}
    assert str(TODAY) in dates
    assert str(YESTERDAY) in dates
    # Today has 2 items
    today_group = next(g for g in body if g["date"] == str(TODAY))
    assert len(today_group["items"]) == 2


@patch("azure_cost_cli.api.routes.daily_costs.make_retriever")
@patch("azure_cost_cli.api.routes.daily_costs.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.daily_costs.get_scope")
def test_daily_costs_empty(mock_scope, mock_sub, mock_make):
    mock_scope.return_value = Scope.subscription(SUB_ID)
    mock_make.return_value = _mock_retriever(daily=[])

    resp = client.get(f"/daily-costs?subscription={SUB_ID}")
    assert resp.status_code == 200
    assert resp.json() == []


# ---------------------------------------------------------------------------
# /cost-by-resource
# ---------------------------------------------------------------------------

def _make_resource(**kwargs):
    defaults = dict(
        cost=200.0, cost_usd=220.0, resource_id="/subscriptions/x/rg/my-rg/vm/my-vm",
        resource_type="Microsoft.Compute/virtualMachines", resource_location="westeurope",
        charge_type="Usage", resource_group_name="my-rg", publisher_type="Azure",
        service_name="Virtual Machines", service_tier="D2s v3", meter="D2s v3",
        currency="EUR",
    )
    defaults.update(kwargs)
    return CostResourceItem(**defaults)


@patch("azure_cost_cli.api.routes.cost_by_resource.make_retriever")
@patch("azure_cost_cli.api.routes.cost_by_resource.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.cost_by_resource.get_scope")
def test_cost_by_resource(mock_scope, mock_sub, mock_make):
    mock_scope.return_value = Scope.subscription(SUB_ID)
    resources = [_make_resource(), _make_resource(cost=50.0, cost_usd=55.0, resource_id="/subscriptions/x/rg/my-rg/vm/vm2")]
    mock_make.return_value = _mock_retriever(resources=resources)

    resp = client.get(f"/cost-by-resource?subscription={SUB_ID}")
    assert resp.status_code == 200
    body = resp.json()
    assert body["totalCount"] == 2
    assert body["totalCost"] == pytest.approx(250.0)
    assert len(body["resources"]) == 2
    # Default sort is cost DESC, so first resource has cost=200
    assert body["resources"][0]["cost"] == pytest.approx(200.0)


@patch("azure_cost_cli.api.routes.cost_by_resource.make_retriever")
@patch("azure_cost_cli.api.routes.cost_by_resource.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.cost_by_resource.get_scope")
def test_cost_by_resource_top(mock_scope, mock_sub, mock_make):
    mock_scope.return_value = Scope.subscription(SUB_ID)
    resources = [
        _make_resource(cost=300.0, resource_id="/subscriptions/x/vm/a"),
        _make_resource(cost=200.0, resource_id="/subscriptions/x/vm/b"),
        _make_resource(cost=100.0, resource_id="/subscriptions/x/vm/c"),
    ]
    mock_make.return_value = _mock_retriever(resources=resources)

    resp = client.get(f"/cost-by-resource?subscription={SUB_ID}&top=2")
    assert resp.status_code == 200
    body = resp.json()
    assert len(body["resources"]) == 2


# ---------------------------------------------------------------------------
# /cost-by-tag
# ---------------------------------------------------------------------------

@patch("azure_cost_cli.api.routes.cost_by_tag.make_retriever")
@patch("azure_cost_cli.api.routes.cost_by_tag.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.cost_by_tag.get_scope")
def test_cost_by_tag(mock_scope, mock_sub, mock_make):
    mock_scope.return_value = Scope.subscription(SUB_ID)
    r1 = _make_resource(tags={"environment": "prod"})
    r2 = _make_resource(cost=50.0, cost_usd=55.0, resource_id="/x/vm/b", tags={"environment": "dev"})
    mock_make.return_value = _mock_retriever(resources=[r1, r2])

    resp = client.get(f"/cost-by-tag?subscription={SUB_ID}&tags=environment")
    assert resp.status_code == 200
    body = resp.json()
    assert len(body) == 1
    group = body[0]
    assert group["tag"] == "environment"
    values = {v["value"]: v["cost"] for v in group["values"]}
    assert "prod" in values
    assert "dev" in values


@patch("azure_cost_cli.api.routes.cost_by_tag.make_retriever")
@patch("azure_cost_cli.api.routes.cost_by_tag.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.cost_by_tag.get_scope")
def test_cost_by_tag_untagged(mock_scope, mock_sub, mock_make):
    mock_scope.return_value = Scope.subscription(SUB_ID)
    r1 = _make_resource(tags={})
    mock_make.return_value = _mock_retriever(resources=[r1])

    resp = client.get(f"/cost-by-tag?subscription={SUB_ID}&tags=env&includeUntagged=true")
    assert resp.status_code == 200
    body = resp.json()
    assert body[0]["values"][0]["value"] == "(untagged)"


# ---------------------------------------------------------------------------
# /budgets
# ---------------------------------------------------------------------------

@patch("azure_cost_cli.api.routes.budgets.make_retriever")
@patch("azure_cost_cli.api.routes.budgets.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.budgets.get_scope")
def test_budgets(mock_scope, mock_sub, mock_make):
    mock_scope.return_value = Scope.subscription(SUB_ID)
    budget = BudgetItem(
        name="monthly-budget",
        id="/subscriptions/x/budgets/monthly-budget",
        amount=1000.0,
        time_grain="Monthly",
        start_date=datetime(2024, 1, 1),
        end_date=datetime(2024, 12, 31),
        current_spend_amount=450.0,
        current_spend_currency="EUR",
        forecast_amount=900.0,
        forecast_currency="EUR",
        notifications=[
            Notification(name="n1", enabled=True, operator="GreaterThan",
                         threshold=80.0, contact_emails=["admin@example.com"])
        ],
    )
    mock_make.return_value = _mock_retriever(budgets=[budget])

    resp = client.get(f"/budgets?subscription={SUB_ID}")
    assert resp.status_code == 200
    body = resp.json()
    assert len(body) == 1
    b = body[0]
    assert b["name"] == "monthly-budget"
    assert b["amount"] == 1000.0
    assert b["currentSpendAmount"] == 450.0
    assert b["currentSpendPercentage"] == pytest.approx(45.0)
    assert len(b["notifications"]) == 1
    assert b["notifications"][0]["name"] == "n1"


@patch("azure_cost_cli.api.routes.budgets.make_retriever")
@patch("azure_cost_cli.api.routes.budgets.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.budgets.get_scope")
def test_budgets_empty(mock_scope, mock_sub, mock_make):
    mock_scope.return_value = Scope.subscription(SUB_ID)
    mock_make.return_value = _mock_retriever(budgets=[])

    resp = client.get(f"/budgets?subscription={SUB_ID}")
    assert resp.status_code == 200
    assert resp.json() == []


# ---------------------------------------------------------------------------
# /detect-anomalies
# ---------------------------------------------------------------------------

@patch("azure_cost_cli.api.routes.detect_anomalies.make_retriever")
@patch("azure_cost_cli.api.routes.detect_anomalies.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.detect_anomalies.get_scope")
def test_detect_anomalies(mock_scope, mock_sub, mock_make):
    mock_scope.return_value = Scope.subscription(SUB_ID)
    # Build 10-day daily costs, last 2 days have spike
    daily = [
        CostDailyItem(date=date(2024, 3, d), name="rg1", cost=float(d * 10), cost_usd=float(d * 11), currency="EUR")
        for d in range(1, 11)
    ]
    mock_make.return_value = _mock_retriever(daily=daily)

    resp = client.get(f"/detect-anomalies?subscription={SUB_ID}")
    assert resp.status_code == 200
    body = resp.json()
    # Result is a list (may be empty if thresholds not met, but should be a list)
    assert isinstance(body, list)


@patch("azure_cost_cli.api.routes.detect_anomalies.make_retriever")
@patch("azure_cost_cli.api.routes.detect_anomalies.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.detect_anomalies.get_scope")
def test_detect_anomalies_empty(mock_scope, mock_sub, mock_make):
    mock_scope.return_value = Scope.subscription(SUB_ID)
    mock_make.return_value = _mock_retriever(daily=[])

    resp = client.get(f"/detect-anomalies?subscription={SUB_ID}")
    assert resp.status_code == 200
    assert resp.json() == []


# ---------------------------------------------------------------------------
# /diff
# ---------------------------------------------------------------------------

@patch("azure_cost_cli.api.routes.diff.make_retriever")
@patch("azure_cost_cli.api.routes.diff.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.diff.get_scope")
def test_diff(mock_scope, mock_sub, mock_make):
    mock_scope.return_value = Scope.subscription(SUB_ID)
    mock_make.return_value = _mock_retriever()

    resp = client.get(
        f"/diff?subscription={SUB_ID}"
        f"&sourceFrom=2024-01-01&sourceTo=2024-01-31"
        f"&targetFrom=2024-02-01&targetTo=2024-02-29"
    )
    assert resp.status_code == 200
    body = resp.json()
    assert "source" in body
    assert "target" in body
    # Both periods use the same mock data
    assert len(body["source"]["costs"]) == 2
    assert len(body["target"]["costs"]) == 2


# ---------------------------------------------------------------------------
# /regions
# ---------------------------------------------------------------------------

@patch("azure_cost_cli.api.routes.regions.AzureRegionsRetriever")
def test_regions(mock_cls):
    region = AzureRegion(
        id="westeurope", display_name="West Europe", continent="Europe",
        location="westeurope", latitude=52.3667, longitude=4.9, is_open=True,
    )
    instance = MagicMock()
    instance.retrieve_regions = AsyncMock(return_value=[region])
    mock_cls.return_value = instance

    resp = client.get("/regions")
    assert resp.status_code == 200
    body = resp.json()
    assert len(body) == 1
    assert body[0]["id"] == "westeurope"
    assert body[0]["displayName"] == "West Europe"
    assert body[0]["isOpen"] is True


@patch("azure_cost_cli.api.routes.regions.AzureRegionsRetriever")
def test_regions_empty(mock_cls):
    instance = MagicMock()
    instance.retrieve_regions = AsyncMock(return_value=[])
    mock_cls.return_value = instance

    resp = client.get("/regions")
    assert resp.status_code == 200
    assert resp.json() == []


# ---------------------------------------------------------------------------
# /what-if/region and /what-if/devtest – just check 200 with empty usage
# ---------------------------------------------------------------------------

@patch("azure_cost_cli.api.routes.whatif.AzurePriceRetriever")
@patch("azure_cost_cli.api.routes.whatif.make_retriever")
@patch("azure_cost_cli.api.routes.whatif.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.whatif.get_scope")
def test_whatif_region_no_vms(mock_scope, mock_sub, mock_make, mock_price_cls):
    mock_scope.return_value = Scope.subscription(SUB_ID)
    mock_make.return_value = _mock_retriever(usage=[])
    price_instance = MagicMock()
    price_instance.get_azure_prices = AsyncMock(return_value=[])
    price_instance.close = AsyncMock()
    mock_price_cls.return_value = price_instance

    resp = client.get(f"/what-if/region?subscription={SUB_ID}")
    assert resp.status_code == 200
    assert resp.json() == []


@patch("azure_cost_cli.api.routes.whatif.AzurePriceRetriever")
@patch("azure_cost_cli.api.routes.whatif.make_retriever")
@patch("azure_cost_cli.api.routes.whatif.get_resolved_subscription", return_value=SUB_ID)
@patch("azure_cost_cli.api.routes.whatif.get_scope")
def test_whatif_devtest_no_vms(mock_scope, mock_sub, mock_make, mock_price_cls):
    mock_scope.return_value = Scope.subscription(SUB_ID)
    mock_make.return_value = _mock_retriever(usage=[])
    price_instance = MagicMock()
    price_instance.get_azure_prices = AsyncMock(return_value=[])
    price_instance.close = AsyncMock()
    mock_price_cls.return_value = price_instance

    resp = client.get(f"/what-if/devtest?subscription={SUB_ID}")
    assert resp.status_code == 200
    assert resp.json() == []


# ---------------------------------------------------------------------------
# OpenAPI schema sanity check
# ---------------------------------------------------------------------------

def test_openapi_schema():
    resp = client.get("/openapi.json")
    assert resp.status_code == 200
    schema = resp.json()
    paths = set(schema["paths"].keys())
    expected = {
        "/accumulated-cost", "/daily-costs", "/cost-by-resource",
        "/cost-by-tag", "/budgets", "/detect-anomalies",
        "/diff", "/regions", "/what-if/region", "/what-if/devtest", "/health",
    }
    assert expected <= paths


def test_docs_available():
    resp = client.get("/docs")
    assert resp.status_code == 200
