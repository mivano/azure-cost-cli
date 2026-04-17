"""GET /diff – mirrors the diff CLI command (live date-range comparison).

POST /diff/files – file-based diff (accepts two JSON cost export files).
"""
from __future__ import annotations

import calendar
from datetime import date, datetime, timezone
from typing import Annotated, Optional

from fastapi import APIRouter, HTTPException, Query

from azure_cost_cli.api.dependencies import (
    get_from_date,
    get_scope,
    get_resolved_subscription,
    get_to_date,
    make_retriever,
)
from azure_cost_cli.api.schemas import CostItemResponse, CostNamedItemResponse, DiffCostResponse
from azure_cost_cli.models import AccumulatedCostDetails, MetricType, TimeframeType

router = APIRouter(prefix="/diff", tags=["Cost Diff"])


def _to_diff_response(details: AccumulatedCostDetails) -> DiffCostResponse:
    return DiffCostResponse(
        costs=[
            CostItemResponse(date=c.date, cost=c.cost, costUsd=c.cost_usd, currency=c.currency)
            for c in sorted(details.costs, key=lambda x: x.date)
        ],
        forecastedCosts=[
            CostItemResponse(date=c.date, cost=c.cost, costUsd=c.cost_usd, currency=c.currency)
            for c in sorted(details.forecasted_costs, key=lambda x: x.date)
        ],
        byServiceNames=[
            CostNamedItemResponse(name=c.item_name, cost=c.cost, costUsd=c.cost_usd, currency=c.currency)
            for c in sorted(details.by_service_name_costs, key=lambda x: x.cost, reverse=True)
        ],
        byLocation=[
            CostNamedItemResponse(name=c.item_name, cost=c.cost, costUsd=c.cost_usd, currency=c.currency)
            for c in sorted(details.by_location_costs, key=lambda x: x.cost, reverse=True)
        ],
        byResourceGroup=[
            CostNamedItemResponse(name=c.item_name, cost=c.cost, costUsd=c.cost_usd, currency=c.currency)
            for c in sorted(details.by_resource_group_costs, key=lambda x: x.cost, reverse=True)
        ],
    )


from pydantic import BaseModel


class DiffLiveResponse(BaseModel):
    source: DiffCostResponse
    target: DiffCostResponse


async def _fetch_details(
    retriever, scope, filters: list[str], mt: MetricType, from_d: date, to_d: date
) -> AccumulatedCostDetails:
    tf = TimeframeType.CUSTOM
    costs = await retriever.retrieve_costs(scope, filters, mt, tf, from_d, to_d)

    today = datetime.now(timezone.utc).date()
    forecasted = []
    if to_d >= today:
        last_day = calendar.monthrange(to_d.year, to_d.month)[1]
        fc_end = to_d.replace(day=last_day)
        forecasted = await retriever.retrieve_forecasted_costs(scope, filters, mt, tf, today, fc_end)

    by_sub = None
    if not scope.is_subscription_based:
        by_sub = await retriever.retrieve_cost_by_subscription(scope, filters, mt, tf, from_d, to_d)

    by_svc = await retriever.retrieve_cost_by_service_name(scope, filters, mt, tf, from_d, to_d)
    by_loc = await retriever.retrieve_cost_by_location(scope, filters, mt, tf, from_d, to_d)
    by_rg = await retriever.retrieve_cost_by_resource_group(scope, filters, mt, tf, from_d, to_d)

    return AccumulatedCostDetails(
        subscription=None, enrollment_account=None,
        costs=costs, forecasted_costs=forecasted,
        by_service_name_costs=by_svc, by_location_costs=by_loc,
        by_resource_group_costs=by_rg, by_subscription_costs=by_sub,
    )


@router.get("", response_model=DiffLiveResponse, summary="Compare costs between two date ranges (live API)")
async def get_diff(
    subscription: Annotated[Optional[str], Query(description="Azure subscription ID")] = None,
    resource_group: Annotated[Optional[str], Query(alias="resourceGroup")] = None,
    billing_account: Annotated[Optional[str], Query(alias="billingAccount")] = None,
    enrollment_account: Annotated[Optional[str], Query(alias="enrollmentAccount")] = None,
    source_from: Annotated[date, Query(alias="sourceFrom", description="Source period start date YYYY-MM-DD")] = ...,
    source_to: Annotated[date, Query(alias="sourceTo", description="Source period end date YYYY-MM-DD")] = ...,
    target_from: Annotated[Optional[date], Query(alias="targetFrom", description="Target period start (default: start of prev month)")] = None,
    target_to: Annotated[Optional[date], Query(alias="targetTo", description="Target period end (default: today)")] = None,
    metric: Annotated[str, Query()] = "ActualCost",
    filters: Annotated[list[str], Query()] = [],
    cost_api_base_address: Annotated[str, Query(alias="costApiBaseAddress")] = "https://management.azure.com/",
    http_timeout: Annotated[int, Query(alias="httpTimeout")] = 100,
    debug: bool = False,
):
    scope = get_scope(subscription, resource_group, billing_account, enrollment_account)
    resolved_sub = get_resolved_subscription(subscription, resource_group, billing_account, enrollment_account)
    scope = get_scope(resolved_sub, resource_group, billing_account, enrollment_account)

    from_d = get_from_date(target_from)
    to_d = get_to_date(target_to)
    mt = MetricType(metric)

    retriever = make_retriever(cost_api_base_address, http_timeout, debug)
    try:
        source_details = await _fetch_details(retriever, scope, list(filters), mt, source_from, source_to)
        target_details = await _fetch_details(retriever, scope, list(filters), mt, from_d, to_d)
    except Exception as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    finally:
        await retriever.close()

    return DiffLiveResponse(
        source=_to_diff_response(source_details),
        target=_to_diff_response(target_details),
    )
