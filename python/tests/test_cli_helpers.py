"""Tests for CLI helpers: scope creation, subscription resolution, threshold."""
from __future__ import annotations

import pytest
from unittest.mock import patch, MagicMock

from azure_cost_cli.cli import (
    _get_scope,
    _apply_auto_timeframe,
    _get_from_date,
    _get_to_date,
    _get_resources_by_tag,
    _check_threshold,
)
from azure_cost_cli.models import (
    CostResourceItem,
    MetricType,
    OutputFormat,
    Scope,
    TimeframeType,
)
from datetime import date, datetime, timedelta, timezone


# ---------------------------------------------------------------------------
# _get_scope
# ---------------------------------------------------------------------------

def test_get_scope_subscription_only():
    scope = _get_scope("abc-123", None, None, None)
    assert scope.is_subscription_based is True
    assert "abc-123" in scope.scope_path


def test_get_scope_resource_group():
    scope = _get_scope("abc-123", "my-rg", None, None)
    assert scope.is_subscription_based is True
    assert "my-rg" in scope.scope_path


def test_get_scope_billing_account():
    scope = _get_scope(None, None, "ba-123", None)
    assert scope.is_subscription_based is False
    assert "ba-123" in scope.scope_path


def test_get_scope_enrollment_account():
    scope = _get_scope(None, None, "ba-123", "ea-456")
    assert scope.is_subscription_based is False
    assert "ea-456" in scope.scope_path


def test_get_scope_default_subscription():
    """When subscription is None and no billing info, default empty GUID scope."""
    scope = _get_scope(None, None, None, None)
    assert scope.is_subscription_based is True


# ---------------------------------------------------------------------------
# _apply_auto_timeframe
# ---------------------------------------------------------------------------

def test_apply_auto_timeframe_both_dates():
    result = _apply_auto_timeframe("BillingMonthToDate", date(2024, 1, 1), date(2024, 1, 31))
    assert result == "Custom"


def test_apply_auto_timeframe_no_dates():
    result = _apply_auto_timeframe("BillingMonthToDate", None, None)
    assert result == "BillingMonthToDate"


def test_apply_auto_timeframe_already_custom():
    result = _apply_auto_timeframe("Custom", date(2024, 1, 1), date(2024, 1, 31))
    assert result == "Custom"


def test_apply_auto_timeframe_only_from():
    # Only from_date set → do not change
    result = _apply_auto_timeframe("BillingMonthToDate", date(2024, 1, 1), None)
    assert result == "BillingMonthToDate"


# ---------------------------------------------------------------------------
# _get_from_date / _get_to_date
# ---------------------------------------------------------------------------

def test_get_from_date_explicit():
    d = date(2024, 6, 15)
    assert _get_from_date(d) == d


def test_get_from_date_default():
    today = datetime.now(timezone.utc).date()
    first_of_month = today.replace(day=1)
    last_month_start = (first_of_month - timedelta(days=1)).replace(day=1)
    assert _get_from_date(None) == last_month_start


def test_get_to_date_explicit():
    d = date(2024, 6, 15)
    assert _get_to_date(d) == d


def test_get_to_date_default():
    today = datetime.now(timezone.utc).date()
    assert _get_to_date(None) == today


# ---------------------------------------------------------------------------
# _check_threshold
# ---------------------------------------------------------------------------

def test_check_threshold_not_exceeded():
    code = _check_threshold(100.0, 200.0, "USD")
    assert code == 0


def test_check_threshold_exceeded():
    code = _check_threshold(300.0, 200.0, "USD")
    assert code == 1


def test_check_threshold_no_threshold():
    code = _check_threshold(9999.0, None, "USD")
    assert code == 0


def test_check_threshold_equal():
    # Exactly equal is not exceeded
    code = _check_threshold(200.0, 200.0, "USD")
    assert code == 0


# ---------------------------------------------------------------------------
# _get_resources_by_tag
# ---------------------------------------------------------------------------

def _make_resource(resource_id: str, tags: dict) -> CostResourceItem:
    return CostResourceItem(
        cost=10.0, cost_usd=11.0,
        resource_id=resource_id,
        resource_type="VM", resource_location="westeurope",
        charge_type="Usage", resource_group_name="rg1",
        publisher_type="Azure", service_name=None,
        service_tier=None, meter=None,
        tags=tags, currency="USD",
    )


def test_get_resources_by_tag_basic():
    resources = [
        _make_resource("r1", {"env": "prod"}),
        _make_resource("r2", {"env": "dev"}),
        _make_resource("r3", {"env": "prod"}),
    ]
    result = _get_resources_by_tag(resources, include_untagged=False, tags=["env"])
    assert "prod" in result["env"]
    assert "dev" in result["env"]
    assert len(result["env"]["prod"]) == 2


def test_get_resources_by_tag_untagged_included():
    resources = [
        _make_resource("r1", {"env": "prod"}),
        _make_resource("r2", {}),  # no tag
    ]
    result = _get_resources_by_tag(resources, include_untagged=True, tags=["env"])
    assert "(untagged)" in result["env"]
    assert len(result["env"]["(untagged)"]) == 1


def test_get_resources_by_tag_untagged_excluded():
    resources = [
        _make_resource("r1", {"env": "prod"}),
        _make_resource("r2", {}),
    ]
    result = _get_resources_by_tag(resources, include_untagged=False, tags=["env"])
    assert "(untagged)" not in result["env"]


def test_get_resources_by_tag_multiple_tags():
    resources = [
        _make_resource("r1", {"env": "prod", "team": "infra"}),
        _make_resource("r2", {"team": "dev"}),
    ]
    result = _get_resources_by_tag(resources, include_untagged=False, tags=["env", "team"])
    assert "prod" in result["env"]
    assert "infra" in result["team"]
    assert "dev" in result["team"]


def test_get_resources_by_tag_case_insensitive():
    resources = [
        _make_resource("r1", {"ENV": "PROD"}),
    ]
    result = _get_resources_by_tag(resources, include_untagged=False, tags=["env"])
    assert "PROD" in result["env"]


def test_get_resources_by_tag_empty():
    result = _get_resources_by_tag([], include_untagged=True, tags=["env"])
    assert result == {"env": {}}
