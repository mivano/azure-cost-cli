"""GET /accumulated-cost – mirrors the accumulatedCost CLI command."""
from __future__ import annotations

import calendar
from datetime import date, datetime, timezone
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
from azure_cost_cli.api.schemas import (
    AccumulatedCostResponse,
    AccumulatedTotals,
    CostItemResponse,
    CostNamedItemResponse,
)
from azure_cost_cli.models import MetricType, Subscription, TimeframeType

router = APIRouter(prefix="/accumulated-cost", tags=["Accumulated Cost"])


@router.get("", response_model=AccumulatedCostResponse, summary="Accumulated cost overview")
async def get_accumulated_cost(
    subscription: Annotated[Optional[str], Query(description="Azure subscription ID")] = None,
    resource_group: Annotated[Optional[str], Query(alias="resourceGroup")] = None,
    billing_account: Annotated[Optional[str], Query(alias="billingAccount")] = None,
    enrollment_account: Annotated[Optional[str], Query(alias="enrollmentAccount")] = None,
    timeframe: Annotated[str, Query(description="BillingMonthToDate | Custom | MonthToDate | TheLastBillingMonth | TheLastMonth | WeekToDate")] = "BillingMonthToDate",
    from_date: Annotated[Optional[date], Query(alias="from", description="Start date YYYY-MM-DD")] = None,
    to_date: Annotated[Optional[date], Query(alias="to", description="End date YYYY-MM-DD")] = None,
    metric: Annotated[str, Query(description="ActualCost | AmortizedCost")] = "ActualCost",
    filters: Annotated[list[str], Query(description="Dimension/tag filter e.g. ResourceGroupName=rg1")] = [],
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
        if scope.is_subscription_based:
            sub = await retriever.retrieve_subscription(resolved_sub)
        else:
            sub = Subscription(subscriptionId=scope.name, displayName=scope.name, state="Active")

        tf = get_timeframe(timeframe, from_date, to_date)
        from_d = get_from_date(from_date)
        to_d = get_to_date(to_date)
        mt = MetricType(metric)

        costs = await retriever.retrieve_costs(scope, filters, mt, tf, from_d, to_d)

        today = datetime.now(timezone.utc).date()
        forecasted_costs = []
        if to_d >= today:
            last_day = calendar.monthrange(to_d.year, to_d.month)[1]
            fc_end = to_d.replace(day=last_day)
            forecasted_costs = await retriever.retrieve_forecasted_costs(
                scope, filters, mt, TimeframeType.CUSTOM, today, fc_end
            )

        by_sub = None
        if not scope.is_subscription_based:
            by_sub = await retriever.retrieve_cost_by_subscription(scope, filters, mt, tf, from_d, to_d)

        by_svc = await retriever.retrieve_cost_by_service_name(scope, filters, mt, tf, from_d, to_d)
        by_loc = await retriever.retrieve_cost_by_location(scope, filters, mt, tf, from_d, to_d)
        by_rg = await retriever.retrieve_cost_by_resource_group(scope, filters, mt, tf, from_d, to_d)

    except Exception as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    finally:
        await retriever.close()

    def pick(item):
        return item.cost_usd if use_usd else item.cost

    yesterday = today.__class__.fromordinal(today.toordinal() - 1)
    seven_ago = today.__class__.fromordinal(today.toordinal() - 7)
    thirty_ago = today.__class__.fromordinal(today.toordinal() - 30)

    return AccumulatedCostResponse(
        totals=AccumulatedTotals(
            todaysCost=sum(pick(c) for c in costs if c.date == today),
            yesterdayCost=sum(pick(c) for c in costs if c.date == yesterday),
            lastSevenDaysCost=sum(pick(c) for c in costs if c.date >= seven_ago),
            lastThirtyDaysCost=sum(pick(c) for c in costs if c.date >= thirty_ago),
            totalCostInTimeframe=sum(pick(c) for c in costs),
        ),
        costs=[
            CostItemResponse(date=c.date, cost=c.cost, costUsd=c.cost_usd, currency=c.currency)
            for c in sorted(costs, key=lambda x: x.date)
        ],
        forecastedCosts=[
            CostItemResponse(date=c.date, cost=c.cost, costUsd=c.cost_usd, currency=c.currency)
            for c in sorted(forecasted_costs, key=lambda x: x.date, reverse=True)
        ],
        byServiceNames=[
            CostNamedItemResponse(name=c.item_name, cost=c.cost, costUsd=c.cost_usd, currency=c.currency)
            for c in sorted(by_svc, key=lambda x: x.cost, reverse=True)
        ],
        byLocation=[
            CostNamedItemResponse(name=c.item_name, cost=c.cost, costUsd=c.cost_usd, currency=c.currency)
            for c in sorted(by_loc, key=lambda x: x.cost, reverse=True)
        ],
        byResourceGroup=[
            CostNamedItemResponse(name=c.item_name, cost=c.cost, costUsd=c.cost_usd, currency=c.currency)
            for c in sorted(by_rg, key=lambda x: x.cost, reverse=True)
        ],
        bySubscription=(
            [
                CostNamedItemResponse(name=c.item_name, cost=c.cost, costUsd=c.cost_usd, currency=c.currency)
                for c in sorted(by_sub, key=lambda x: x.cost, reverse=True)
            ]
            if by_sub is not None else None
        ),
    )
