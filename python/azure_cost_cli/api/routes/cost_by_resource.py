"""GET /cost-by-resource – mirrors the costByResource CLI command."""
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
from azure_cost_cli.api.schemas import CostByResourceResponse, ResourceItem
from azure_cost_cli.models import MetricType

router = APIRouter(prefix="/cost-by-resource", tags=["Cost by Resource"])

_SORT_CHOICES = ["cost", "cost-asc", "name", "resource-group", "resource-type", "location"]


@router.get("", response_model=CostByResourceResponse, summary="Cost breakdown by resource")
async def get_cost_by_resource(
    subscription: Annotated[Optional[str], Query(description="Azure subscription ID")] = None,
    resource_group: Annotated[Optional[str], Query(alias="resourceGroup")] = None,
    billing_account: Annotated[Optional[str], Query(alias="billingAccount")] = None,
    enrollment_account: Annotated[Optional[str], Query(alias="enrollmentAccount")] = None,
    timeframe: Annotated[str, Query()] = "BillingMonthToDate",
    from_date: Annotated[Optional[date], Query(alias="from")] = None,
    to_date: Annotated[Optional[date], Query(alias="to")] = None,
    metric: Annotated[str, Query()] = "ActualCost",
    filters: Annotated[list[str], Query()] = [],
    exclude_meter_details: Annotated[bool, Query(alias="excludeMeterDetails")] = False,
    top: Annotated[int, Query(description="Limit to top N resources by cost (0 = all)")] = 0,
    sort: Annotated[str, Query(description=f"Sort order: {', '.join(_SORT_CHOICES)}")] = "cost",
    use_usd: Annotated[bool, Query(alias="useUsd")] = False,
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
            scope, filters, mt, exclude_meter_details, tf, from_d, to_d
        )
    except Exception as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    finally:
        await retriever.close()

    # Sort
    sort_lower = sort.lower()
    if sort_lower == "cost-asc":
        resources = sorted(resources, key=lambda r: r.cost_usd if use_usd else r.cost)
    elif sort_lower == "name":
        resources = sorted(resources, key=lambda r: r.resource_id)
    elif sort_lower == "resource-group":
        resources = sorted(resources, key=lambda r: r.resource_group_name)
    elif sort_lower == "resource-type":
        resources = sorted(resources, key=lambda r: r.resource_type)
    elif sort_lower == "location":
        resources = sorted(resources, key=lambda r: r.resource_location)
    else:
        resources = sorted(resources, key=lambda r: r.cost_usd if use_usd else r.cost, reverse=True)

    total_count = len({r.resource_id for r in resources})
    total_cost = sum(r.cost_usd if use_usd else r.cost for r in resources)
    currency = resources[0].currency if resources else "USD"

    if top > 0:
        sorted_by_cost = sorted(resources, key=lambda r: r.cost_usd if use_usd else r.cost, reverse=True)
        top_ids = {r.resource_id for r in sorted_by_cost[:top]}
        resources = [r for r in resources if r.resource_id in top_ids]

    return CostByResourceResponse(
        totalCount=total_count,
        totalCost=total_cost,
        currency=currency,
        resources=[
            ResourceItem(
                resourceId=r.resource_id,
                resourceName=r.resource_name,
                resourceType=r.resource_type,
                resourceLocation=r.resource_location,
                resourceGroupName=r.resource_group_name,
                chargeType=r.charge_type,
                publisherType=r.publisher_type,
                serviceName=r.service_name,
                serviceTier=r.service_tier,
                meter=r.meter,
                cost=r.cost,
                costUsd=r.cost_usd,
                currency=r.currency,
                tags=r.tags,
            )
            for r in resources
        ],
    )
