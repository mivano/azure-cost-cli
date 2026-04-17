"""Shared FastAPI dependencies (common query parameters injection)."""
from __future__ import annotations

from datetime import date, datetime, timedelta, timezone
from typing import Annotated, Optional

from fastapi import Query

from azure_cost_cli.models import MetricType, Scope, TimeframeType


def _resolve_subscription_dep(subscription: Optional[str], scope: Scope) -> str:
    if subscription:
        return subscription
    if not scope.is_subscription_based:
        return ""
    try:
        import subprocess
        result = subprocess.run(
            ["az", "account", "show", "--query", "id", "-o", "tsv"],
            capture_output=True, text=True, check=True,
        )
        return result.stdout.strip()
    except Exception as exc:
        from fastapi import HTTPException
        raise HTTPException(
            status_code=400,
            detail="No subscription provided and unable to retrieve from Azure CLI. "
                   "Pass ?subscription=<id> or run 'az login'.",
        ) from exc


def get_scope(
    subscription: Optional[str],
    resource_group: Optional[str],
    billing_account: Optional[str],
    enrollment_account: Optional[str],
) -> Scope:
    from azure_cost_cli.cli import _get_scope
    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)
    resolved = _resolve_subscription_dep(subscription, scope)
    return _get_scope(resolved, resource_group, billing_account, enrollment_account)


def get_resolved_subscription(
    subscription: Optional[str],
    resource_group: Optional[str],
    billing_account: Optional[str],
    enrollment_account: Optional[str],
) -> str:
    from azure_cost_cli.cli import _get_scope
    scope = _get_scope(subscription, resource_group, billing_account, enrollment_account)
    return _resolve_subscription_dep(subscription, scope)


def get_from_date(from_date: Optional[date]) -> date:
    if from_date:
        return from_date
    first_of_month = datetime.now(timezone.utc).date().replace(day=1)
    return (first_of_month - timedelta(days=1)).replace(day=1)


def get_to_date(to_date: Optional[date]) -> date:
    return to_date or datetime.now(timezone.utc).date()


def get_timeframe(timeframe: str, from_date: Optional[date], to_date: Optional[date]) -> TimeframeType:
    if from_date and to_date and timeframe != TimeframeType.CUSTOM.value:
        return TimeframeType.CUSTOM
    return TimeframeType(timeframe)


def make_retriever(
    cost_api_base_address: str = "https://management.azure.com/",
    http_timeout: int = 100,
    debug: bool = False,
):
    from azure_cost_cli.cost_api import AzureCostApiRetriever
    return AzureCostApiRetriever(
        cost_api_address=cost_api_base_address,
        http_timeout=http_timeout,
        debug=debug,
    )
