"""GET /detect-anomalies – mirrors the detectAnomalies CLI command."""
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
from azure_cost_cli.api.schemas import AnomalyResponse
from azure_cost_cli.models import MetricType

router = APIRouter(prefix="/detect-anomalies", tags=["Anomaly Detection"])


@router.get("", response_model=list[AnomalyResponse], summary="Detect cost anomalies and trends")
async def get_detect_anomalies(
    subscription: Annotated[Optional[str], Query(description="Azure subscription ID")] = None,
    resource_group: Annotated[Optional[str], Query(alias="resourceGroup")] = None,
    billing_account: Annotated[Optional[str], Query(alias="billingAccount")] = None,
    enrollment_account: Annotated[Optional[str], Query(alias="enrollmentAccount")] = None,
    timeframe: Annotated[str, Query()] = "BillingMonthToDate",
    from_date: Annotated[Optional[date], Query(alias="from")] = None,
    to_date: Annotated[Optional[date], Query(alias="to")] = None,
    metric: Annotated[str, Query()] = "ActualCost",
    filters: Annotated[list[str], Query()] = [],
    dimension: Annotated[str, Query()] = "ResourceGroupName",
    recent_activity_days: Annotated[int, Query(alias="recentActivityDays")] = 7,
    significant_change: Annotated[float, Query(alias="significantChange")] = 0.75,
    steady_growth_days: Annotated[int, Query(alias="steadyGrowthDays")] = 7,
    threshold_cost: Annotated[float, Query(alias="thresholdCost")] = 2.0,
    exclude_removed_costs: Annotated[bool, Query(alias="excludeRemovedCosts")] = False,
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
            scope, filters, mt, dimension, tf, from_d, to_d, False
        )
    except Exception as exc:
        raise HTTPException(status_code=502, detail=str(exc)) from exc
    finally:
        await retriever.close()

    from azure_cost_cli.commands.cost_analyzer import analyze_cost

    anomalies = analyze_cost(
        daily,
        recent_activity_days=recent_activity_days,
        significant_change=significant_change,
        steady_growth_days=steady_growth_days,
        threshold_cost=threshold_cost,
        exclude_removed_costs=exclude_removed_costs,
    )

    return [
        AnomalyResponse(
            name=a.name,
            detectionDate=a.detection_date,
            message=a.message,
            costDifference=a.cost_difference,
            anomalyType=a.anomaly_type,
        )
        for a in anomalies
    ]
