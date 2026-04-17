"""GET /cost-by-tag – mirrors the costByTag CLI command."""
from __future__ import annotations

from datetime import date
from typing import Annotated, Optional

from fastapi import APIRouter, HTTPException, Query

from azure_cost_cli.api.dependencies import (
    get_from_date,
    get_scope,
    get_resolved_subscription,
    get_timeframe,
    get_to_date,
    make_retriever,
)
from azure_cost_cli.api.schemas import TagGroupResponse, TagValueSummary
from azure_cost_cli.models import MetricType

router = APIRouter(prefix="/cost-by-tag", tags=["Cost by Tag"])


@router.get("", response_model=list[TagGroupResponse], summary="Cost grouped by tag key(s)")
async def get_cost_by_tag(
    subscription: Annotated[Optional[str], Query(description="Azure subscription ID")] = None,
    resource_group: Annotated[Optional[str], Query(alias="resourceGroup")] = None,
    billing_account: Annotated[Optional[str], Query(alias="billingAccount")] = None,
    enrollment_account: Annotated[Optional[str], Query(alias="enrollmentAccount")] = None,
    timeframe: Annotated[str, Query()] = "BillingMonthToDate",
    from_date: Annotated[Optional[date], Query(alias="from")] = None,
    to_date: Annotated[Optional[date], Query(alias="to")] = None,
    metric: Annotated[str, Query()] = "ActualCost",
    filters: Annotated[list[str], Query()] = [],
    tags: Annotated[list[str], Query(description="Tag key(s) to group by e.g. environment")] = [],
    include_untagged: Annotated[bool, Query(alias="includeUntagged")] = True,
    cost_api_base_address: Annotated[str, Query(alias="costApiBaseAddress")] = "https://management.azure.com/",
    http_timeout: Annotated[int, Query(alias="httpTimeout")] = 100,
    debug: bool = False,
):
    scope = get_scope(subscription, resource_group, billing_account, enrollment_account)
    resolved_sub = get_resolved_subscription(subscription, resource_group, billing_account, enrollment_account)
    scope = get_scope(resolved_sub, resource_group, billing_account, enrollment_account)

    retriever = make_retriever(cost_api_base_address, http_timeout, debug)
    try:
        tf = get_timeframe(timeframe, from_date, to_date)
        from_d = get_from_date(from_date)
        to_d = get_to_date(to_date)
        mt = MetricType(metric)

        resources = await retriever.retrieve_cost_for_resources(
            scope, filters, mt, True, tf, from_d, to_d
        )
    except Exception as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    finally:
        await retriever.close()

    from azure_cost_cli.cli import _get_resources_by_tag

    by_tags = _get_resources_by_tag(resources, include_untagged, list(tags))

    result: list[TagGroupResponse] = []
    for tag, values in by_tags.items():
        summaries: list[TagValueSummary] = []
        for val, items in values.items():
            total_cost = sum(r.cost for r in items)
            currency = items[0].currency if items else "USD"
            summaries.append(TagValueSummary(value=val, cost=total_cost, currency=currency))
        summaries.sort(key=lambda x: x.cost, reverse=True)
        result.append(TagGroupResponse(tag=tag, values=summaries))

    return result
