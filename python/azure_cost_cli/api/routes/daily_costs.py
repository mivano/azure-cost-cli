"""GET /daily-costs – mirrors the dailyCosts CLI command."""
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
from azure_cost_cli.api.schemas import DailyCostGroup, DailyCostItem
from azure_cost_cli.models import MetricType

router = APIRouter(prefix="/daily-costs", tags=["Daily Costs"])


@router.get("", response_model=list[DailyCostGroup], summary="Daily costs grouped by a dimension")
async def get_daily_costs(
    subscription: Annotated[Optional[str], Query(description="Azure subscription ID")] = None,
    resource_group: Annotated[Optional[str], Query(alias="resourceGroup")] = None,
    billing_account: Annotated[Optional[str], Query(alias="billingAccount")] = None,
    enrollment_account: Annotated[Optional[str], Query(alias="enrollmentAccount")] = None,
    timeframe: Annotated[str, Query()] = "BillingMonthToDate",
    from_date: Annotated[Optional[date], Query(alias="from")] = None,
    to_date: Annotated[Optional[date], Query(alias="to")] = None,
    metric: Annotated[str, Query()] = "ActualCost",
    filters: Annotated[list[str], Query()] = [],
    dimension: Annotated[str, Query(description="Grouping dimension e.g. ResourceGroupName, ServiceName")] = "ResourceGroupName",
    include_tags: Annotated[bool, Query(alias="includeTags")] = False,
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

        daily = await retriever.retrieve_daily_cost(
            scope, filters, mt, dimension, tf, from_d, to_d, include_tags
        )
    except Exception as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    finally:
        await retriever.close()

    from itertools import groupby as _groupby

    sorted_daily = sorted(daily, key=lambda x: x.date)
    groups: list[DailyCostGroup] = []
    for d, group_iter in _groupby(sorted_daily, key=lambda x: x.date):
        items = [
            DailyCostItem(
                date=item.date,
                name=item.name,
                cost=item.cost,
                costUsd=item.cost_usd,
                currency=item.currency,
                tags=item.tags,
            )
            for item in group_iter
        ]
        groups.append(DailyCostGroup(date=d, items=items))
    return groups
