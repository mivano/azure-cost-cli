"""Tests for the resolve_timeframe utility and filter generation."""
from __future__ import annotations

import pytest
from datetime import date

from azure_cost_cli.cost_api import _generate_filters, resolve_timeframe
from azure_cost_cli.models import TimeframeType


# ---------------------------------------------------------------------------
# resolve_timeframe
# ---------------------------------------------------------------------------

def test_resolve_billing_month_to_date_stays():
    tf, from_d, to_d = resolve_timeframe(
        TimeframeType.BILLING_MONTH_TO_DATE,
        date(2024, 1, 1), date(2024, 1, 31),
        date(2024, 1, 15),
    )
    assert tf == TimeframeType.BILLING_MONTH_TO_DATE
    assert from_d == date(2024, 1, 1)
    assert to_d == date(2024, 1, 31)


def test_resolve_custom_stays():
    tf, from_d, to_d = resolve_timeframe(
        TimeframeType.CUSTOM,
        date(2024, 3, 1), date(2024, 3, 15),
        date(2024, 3, 20),
    )
    assert tf == TimeframeType.CUSTOM
    assert from_d == date(2024, 3, 1)
    assert to_d == date(2024, 3, 15)


def test_resolve_the_last_month():
    today = date(2024, 3, 15)
    tf, from_d, to_d = resolve_timeframe(
        TimeframeType.THE_LAST_MONTH,
        date(2024, 1, 1), date(2024, 1, 31),
        today,
    )
    assert tf == TimeframeType.CUSTOM
    assert from_d == date(2024, 2, 1)
    assert to_d == date(2024, 2, 29)


def test_resolve_the_last_billing_month():
    today = date(2024, 5, 10)
    tf, from_d, to_d = resolve_timeframe(
        TimeframeType.THE_LAST_BILLING_MONTH,
        date(2024, 1, 1), date(2024, 1, 31),
        today,
    )
    assert tf == TimeframeType.CUSTOM
    assert from_d == date(2024, 4, 1)
    assert to_d == date(2024, 4, 30)


def test_resolve_last_month_jan():
    """December has 31 days."""
    today = date(2024, 1, 5)
    tf, from_d, to_d = resolve_timeframe(
        TimeframeType.THE_LAST_MONTH,
        date(2023, 1, 1), date(2023, 1, 31),
        today,
    )
    assert tf == TimeframeType.CUSTOM
    assert from_d == date(2023, 12, 1)
    assert to_d == date(2023, 12, 31)


# ---------------------------------------------------------------------------
# _generate_filters
# ---------------------------------------------------------------------------

def test_generate_filters_none():
    result = _generate_filters(None)
    assert result is None


def test_generate_filters_empty():
    result = _generate_filters([])
    assert result is None


def test_generate_filters_dimension():
    result = _generate_filters(["ResourceGroupName=rg1"])
    assert result is not None
    assert "Dimensions" in result
    assert result["Dimensions"]["Name"] == "ResourceGroupName"
    assert result["Dimensions"]["Values"] == ["rg1"]


def test_generate_filters_dimension_multiple_values():
    result = _generate_filters(["ResourceGroupName=rg1;rg2"])
    assert result["Dimensions"]["Values"] == ["rg1", "rg2"]


def test_generate_filters_tag():
    result = _generate_filters(["Environment=prod"])
    assert "Tags" in result
    assert result["Tags"]["Name"] == "Environment"


def test_generate_filters_multiple():
    result = _generate_filters(["ResourceGroupName=rg1", "Environment=prod"])
    assert "And" in result
    assert len(result["And"]) == 2


def test_generate_filters_single_wraps_correctly():
    result = _generate_filters(["ServiceName=Compute"])
    assert "Dimensions" in result
